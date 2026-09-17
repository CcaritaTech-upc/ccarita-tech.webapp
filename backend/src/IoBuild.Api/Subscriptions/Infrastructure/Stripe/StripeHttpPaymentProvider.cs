using System.Text.Json;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;

namespace IoBuild.Api.Subscriptions.Infrastructure.Stripe;

public sealed record PaymentCheckoutRequest(int BuilderId, int PlanId, string SuccessUrl, string CancelUrl);
public sealed record PaymentCheckoutSession(string Id, string Url, long AmountInCents);
public sealed record PaymentSessionConfirmation(string SessionId, string Status, int BuilderId, int PlanId);
public sealed record PaymentInvoice(string Id, string Status, long AmountInCents);

public interface IPaymentProvider
{
    Task<PaymentCheckoutSession?> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, StripeIntegrationOptions options, CancellationToken cancellationToken = default);
    Task<PaymentSessionConfirmation?> ConfirmSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentInvoice>?> GetInvoicesAsync(int builderId, CancellationToken cancellationToken = default);
}

public sealed class StripeHttpPaymentProvider(HttpClient client, IConfiguration configuration, IServiceProvider? serviceProvider = null) : IPaymentProvider
{
    public async Task<PaymentCheckoutSession?> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, StripeIntegrationOptions options, CancellationToken cancellationToken = default)
    {
        if (!StripeRestrictedKeyResolver.IsRestrictedKey(options.RestrictedApiKey)) return null;

        if (configuration.GetValue<bool>("Stripe:UseSimulatedPayments") || options.RestrictedApiKey.StartsWith("rk_test_local", StringComparison.Ordinal))
        {
            var sessionId = $"cs_sim_{request.BuilderId}_{request.PlanId}_{Guid.NewGuid():N}";
            var separator = request.SuccessUrl.Contains('?') ? "&" : "?";
            var url = $"{request.SuccessUrl}{separator}session_id={sessionId}";
            return new PaymentCheckoutSession(sessionId, url, 1200);
        }

        var price = configuration[$"Stripe:PlanPrices:{request.PlanId}"];
        var endpoint = Endpoint("/v1/checkout/sessions");
        if (endpoint is null) return null;

        Dictionary<string, string> formFields;
        if (!string.IsNullOrWhiteSpace(price))
        {
            formFields = new Dictionary<string, string>
            {
                ["mode"] = "subscription",
                ["success_url"] = request.SuccessUrl,
                ["cancel_url"] = request.CancelUrl,
                ["line_items[0][price]"] = price,
                ["line_items[0][quantity]"] = "1",
                ["metadata[builder_id]"] = request.BuilderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["metadata[plan_id]"] = request.PlanId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }
        else
        {
            string name;
            long amountInCents;
            string description;

            if (serviceProvider != null)
            {
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetService<IoBuild.Api.Persistence.IoBuildDbContext>();
                var plan = db != null ? await db.Plans.FindAsync([request.PlanId], cancellationToken) : null;
                if (plan != null)
                {
                    name = plan.Name;
                    amountInCents = (long)(plan.Price * 100);
                    description = plan.Description;
                }
                else
                {
                    (name, amountInCents) = GetPlanInfo(request.PlanId);
                    description = $"Suscripción IoBuild Plan {name}";
                }
            }
            else
            {
                (name, amountInCents) = GetPlanInfo(request.PlanId);
                description = $"Suscripción IoBuild Plan {name}";
            }

            var currency = configuration["Stripe:Currency"] ?? "usd";
            var successUrl = request.SuccessUrl.Contains("{CHECKOUT_SESSION_ID}")
                ? request.SuccessUrl
                : $"{request.SuccessUrl.Split('?')[0]}?success=true&session_id={{CHECKOUT_SESSION_ID}}";

            formFields = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["success_url"] = successUrl,
                ["cancel_url"] = request.CancelUrl,
                ["line_items[0][quantity]"] = "1",
                ["line_items[0][price_data][currency]"] = currency,
                ["line_items[0][price_data][unit_amount]"] = amountInCents.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["line_items[0][price_data][product_data][name]"] = $"Plan {name}",
                ["line_items[0][price_data][product_data][description]"] = description,
                ["metadata[builder_id]"] = request.BuilderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["metadata[plan_id]"] = request.PlanId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        using var body = new FormUrlEncodedContent(formFields);
        using var message = AuthorizedRequest(HttpMethod.Post, endpoint, options.RestrictedApiKey);
        message.Content = body;
        return await SendCheckoutAsync(message, cancellationToken);
    }

    public async Task<PaymentSessionConfirmation?> ConfirmSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId.StartsWith("cs_sim_", StringComparison.Ordinal))
        {
            var parts = sessionId.Split('_');
            int builderId = parts.Length > 2 && int.TryParse(parts[2], out var b) ? b : 1;
            int planId = parts.Length > 3 && int.TryParse(parts[3], out var p) ? p : 1;
            return new PaymentSessionConfirmation(sessionId, "paid", builderId, planId);
        }

        var endpoint = Endpoint($"/v1/checkout/sessions/{Uri.EscapeDataString(sessionId)}");
        var key = StripeRestrictedKeyResolver.Resolve(configuration);
        if (endpoint is null || key is null) return null;
        using var message = AuthorizedRequest(HttpMethod.Get, endpoint, key);
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var id) || !root.TryGetProperty("metadata", out var metadata)
                || !metadata.TryGetProperty("builder_id", out var builder) || !metadata.TryGetProperty("plan_id", out var plan)
                || !int.TryParse(builder.GetString(), out var builderId) || !int.TryParse(plan.GetString(), out var planId)) return null;
            var status = root.TryGetProperty("payment_status", out var paymentStatus) ? paymentStatus.GetString() : root.GetProperty("status").GetString();
            return string.IsNullOrWhiteSpace(status) ? null : new PaymentSessionConfirmation(id.GetString()!, status, builderId, planId);
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
    }

    public async Task<IReadOnlyList<PaymentInvoice>?> GetInvoicesAsync(int builderId, CancellationToken cancellationToken = default)
    {
        var key = StripeRestrictedKeyResolver.Resolve(configuration);
        if (key is null) return null;

        if (configuration.GetValue<bool>("Stripe:UseSimulatedPayments"))
        {
            return [new PaymentInvoice($"in_sim_{builderId}", "paid", 1200)];
        }

        var customer = configuration[$"Stripe:BuilderCustomers:{builderId}"];
        var endpoint = string.IsNullOrWhiteSpace(customer) ? null : Endpoint($"/v1/invoices?customer={Uri.EscapeDataString(customer)}&limit=100");
        if (endpoint is null || key is null) return null;
        using var message = AuthorizedRequest(HttpMethod.Get, endpoint, key);
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return null;
            return data.EnumerateArray().Select(invoice => new PaymentInvoice(
                invoice.GetProperty("id").GetString()!,
                invoice.GetProperty("status").GetString()!,
                invoice.TryGetProperty("amount_paid", out var amount) ? amount.GetInt64() : 0)).ToList();
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
    }

    private Uri? Endpoint(string path)
    {
        var baseUrl = configuration["Stripe:ProviderBaseUrl"];
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ? new Uri(baseUri, path) : null;
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, Uri endpoint, string key)
    {
        // Restricted keys only: the resolver guarantees `key` starts with rk_.
        // A configured secret key must never silently replace it on the wire,
        // or the least-privilege discipline is defeated without a trace.
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("Stripe-Version", "2026-05-27.dahlia");
        return request;
    }

    private static (string Name, long AmountInCents) GetPlanInfo(int planId) => planId switch
    {
        1 => ("Starter", 29900),
        2 => ("Professional", 79900),
        3 => ("Enterprise", 129900),
        _ => ($"Plan {planId}", 29900)
    };

    private async Task<PaymentCheckoutSession?> SendCheckoutAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var id) || !root.TryGetProperty("url", out var url)) return null;
            var amount = root.TryGetProperty("amount_total", out var total) ? total.GetInt64() : 0;
            return new PaymentCheckoutSession(id.GetString()!, url.GetString()!, amount);
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
    }
}
