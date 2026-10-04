using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Domain.Model.Aggregates;
using IoBuild.Api.Subscriptions.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Subscriptions.Application.Internal.CommandServices;

public sealed record StripeWebhookRequest(string EventId, string EventType, string Payload, string Signature);

public static class StripeRestrictedKeyResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        var restricted = configuration["Stripe:RestrictedApiKey"];
        if (IsRestrictedKey(restricted)) return restricted;

        var secret = configuration["Stripe:SecretKey"];
        if (IsRestrictedKey(secret)) return secret;

        // Fallback to the simulated local key only when simulation is explicitly
        // enabled. An empty configuration must fail closed: silently simulating
        // payments in production would activate subscriptions without charging.
        if (configuration.GetValue<bool>("Stripe:UseSimulatedPayments"))
        {
            return "rk_test_local";
        }

        return null;
    }

    public static bool IsRestrictedKey(string? key) => !string.IsNullOrWhiteSpace(key) && key.StartsWith("rk_", StringComparison.Ordinal);
}

public sealed record StripeIntegrationOptions(string RestrictedApiKey, bool UsesDynamicPaymentMethods)
{
    public static StripeIntegrationOptions Create(string restrictedApiKey)
    {
        if (!StripeRestrictedKeyResolver.IsRestrictedKey(restrictedApiKey))
            throw new InvalidOperationException("Stripe integrations require a restricted API key.");

        return new StripeIntegrationOptions(restrictedApiKey, UsesDynamicPaymentMethods: true);
    }
}

public sealed class StripeWebhookProcessor(IoBuildDbContext dbContext, string webhookSecret, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    public async Task<bool> ProcessAsync(StripeWebhookRequest request, CancellationToken cancellationToken = default)
    {
        if (!HasValidSignature(request.Payload, request.Signature)) return false;
        if (await dbContext.SubscriptionWebhooks.AnyAsync(webhook => webhook.EventId == request.EventId, cancellationToken)) return true;

        using var document = JsonDocument.Parse(request.Payload);

        dbContext.SubscriptionWebhooks.Add(new SubscriptionWebhook
        {
            EventId = request.EventId,
            EventType = request.EventType,
            ReceivedAt = DateTimeOffset.UtcNow
        });
        if (string.Equals(request.EventType, "checkout.session.completed", StringComparison.Ordinal)
            && TryGetSubscriptionIdentifiers(document.RootElement, out var builderId, out var planId)
            && IsPaid(document.RootElement))
        {
            dbContext.Subscriptions.Add(new Subscription
            {
                BuilderId = builderId,
                PlanId = planId,
                Status = "active"
            });
        }

        try { await dbContext.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.SubscriptionWebhooks.AsNoTracking().AnyAsync(webhook => webhook.EventId == request.EventId, cancellationToken)) return true;
            throw;
        }
    }

    private bool HasValidSignature(string payload, string signature)
    {
        var values = signature.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => part[1], StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(webhookSecret) || !values.TryGetValue("t", out var timestamp) || !values.TryGetValue("v1", out var supplied)
            || !long.TryParse(timestamp, out var unixTimestamp) || Math.Abs((clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(unixTimestamp)).TotalMinutes) > 5) return false;

        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), signedPayload);
        try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(supplied)); }
        catch (FormatException) { return false; }
    }

    private static bool IsPaid(JsonElement root) => !root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var session)
        || !session.TryGetProperty("payment_status", out var status) || string.Equals(status.GetString(), "paid", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetSubscriptionIdentifiers(JsonElement root, out int builderId, out int planId)
    {
        if (root.TryGetProperty("builderId", out var builder) && root.TryGetProperty("planId", out var plan)
            && builder.TryGetInt32(out builderId) && plan.TryGetInt32(out planId)) return true;

        if (root.TryGetProperty("data", out var data) && data.TryGetProperty("object", out var session)
            && session.TryGetProperty("metadata", out var metadata)
            && metadata.TryGetProperty("builder_id", out var legacyBuilder)
            && metadata.TryGetProperty("plan_id", out var legacyPlan)
            && int.TryParse(legacyBuilder.GetString(), out builderId)
            && int.TryParse(legacyPlan.GetString(), out planId)) return true;

        builderId = default;
        planId = default;
        return false;
    }
}
