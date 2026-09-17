using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Persistence;
using IoBuild.Api.Subscriptions.Application.Internal.CommandServices;
using IoBuild.Api.Subscriptions.Domain.Services;
using IoBuild.Api.Subscriptions.Domain.Services.Commands;
using IoBuild.Api.Subscriptions.Domain.Services.Queries;
using IoBuild.Api.Subscriptions.Infrastructure.Stripe;
using IoBuild.Api.Subscriptions.Interfaces.REST.Resources;
using IoBuild.Api.Subscriptions.Interfaces.REST.Transform;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Subscriptions.Interfaces.REST;

public static class SubscriptionsEndpoints
{
    // The JWT carries the user id in ClaimTypes.Sid (see JwtTokenIssuer).
    // In this bounded context the builder id IS the user id: every purchase
    // endpoint requires the caller to act only on their own builder id.
    private static bool OwnsBuilder(System.Security.Claims.ClaimsPrincipal user, int builderId) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) && id == builderId;

    private static int SelfId(System.Security.Claims.ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) ? id : 0;

    private static System.Text.Json.JsonDocument? ParseWebhookPayload(string payload)
    {
        try
        {
            return System.Text.Json.JsonDocument.Parse(payload);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static void MapSubscriptionsEndpoints(this WebApplication app)
    {
        // ── Plans Endpoints ──
        var plans = app.MapGroup("/api/v1/plans").WithTags("Plans");

        plans.MapGet("", async (IPlanQueryService queryService, CancellationToken ct) =>
        {
            var planList = await queryService.Handle(new GetAllPlansQuery(), ct);
            return Results.Ok(planList.Select(PlanResourceFromEntityAssembler.ToResourceFromEntity));
        }).AllowAnonymous();

        plans.MapGet("/{id:int}", async (int id, IPlanQueryService queryService, CancellationToken ct) =>
        {
            var plan = await queryService.Handle(new GetPlanByIdQuery(id), ct);
            return plan is null ? Results.NotFound() : Results.Ok(PlanResourceFromEntityAssembler.ToResourceFromEntity(plan));
        }).AllowAnonymous();

        plans.MapPost("", async (CreatePlanResource resource, IPlanCommandService commandService, IPlanQueryService queryService, CancellationToken ct) =>
        {
            var command = new CreatePlanCommand(resource.Name, resource.Description, resource.Price, resource.Interval, resource.FeaturesJson);
            var planId = await commandService.Handle(command, ct);
            var created = await queryService.Handle(new GetPlanByIdQuery(planId), ct);
            return created is null ? Results.Problem(statusCode: 500) : Results.Created($"/api/v1/plans/{planId}", PlanResourceFromEntityAssembler.ToResourceFromEntity(created));
        }).RequireAuthorization();

        plans.MapPut("/{id:int}", async (int id, UpdatePlanResource resource, IPlanCommandService commandService, CancellationToken ct) =>
        {
            try
            {
                var command = new UpdatePlanCommand(id, resource.Name, resource.Description, resource.Price, resource.Interval, resource.FeaturesJson);
                await commandService.Handle(command, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();

        // ── Subscriptions Endpoints ──
        var subs = app.MapGroup("/api/v1").WithTags("Subscriptions");

        subs.MapGet("/subscriptions", async (System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var subscriptions = await db.Subscriptions.Where(s => s.BuilderId == SelfId(user)).ToListAsync(ct);
            var plans = await db.Plans.ToDictionaryAsync(p => p.Id, ct);
            var result = subscriptions.Select(s => new
            {
                s.Id,
                s.BuilderId,
                s.PlanId,
                s.Status,
                s.StartDate,
                s.EndDate,
                Plan = plans.TryGetValue(s.PlanId, out var plan) ? new
                {
                    plan.Id,
                    plan.Name,
                    plan.Price,
                    plan.Description,
                    Features = !string.IsNullOrWhiteSpace(plan.FeaturesJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson)
                        : new List<string>()
                } : null
            });
            return Results.Ok(result);
        }).RequireAuthorization();
        subs.MapGet("/subscriptions/{id:int}", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var s = await db.Subscriptions.FindAsync([id], ct);
            if (s is null || !OwnsBuilder(user, s.BuilderId)) return Results.NotFound();
            var plan = await db.Plans.FindAsync([s.PlanId], ct);
            return Results.Ok(new
            {
                s.Id,
                s.BuilderId,
                s.PlanId,
                s.Status,
                s.StartDate,
                s.EndDate,
                Plan = plan is not null ? new
                {
                    plan.Id,
                    plan.Name,
                    plan.Price,
                    plan.Description,
                    Features = !string.IsNullOrWhiteSpace(plan.FeaturesJson)
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(plan.FeaturesJson)
                        : new List<string>()
                } : null
            });
        }).RequireAuthorization();
        subs.MapPut("/subscriptions/{id:int}", async (int id, CreateSubscriptionRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) => { var item = await db.Subscriptions.FindAsync([id], ct); if (item is null || !OwnsBuilder(user, item.BuilderId)) return Results.NotFound(); item.PlanId = request.PlanId; item.EndDate = request.EndDate; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization();
        subs.MapPost("/subscriptions/{id:int}/cancel", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) => { var item = await db.Subscriptions.FindAsync([id], ct); if (item is null || !OwnsBuilder(user, item.BuilderId)) return Results.NotFound(); item.Status = "cancelled"; await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization();
        subs.MapPost("/subscriptions/payments/sessions", async (PaymentCheckoutRequest request, System.Security.Claims.ClaimsPrincipal user, IConfiguration configuration, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!OwnsBuilder(user, request.BuilderId)) return Results.Forbid();
            var restrictedKey = StripeRestrictedKeyResolver.Resolve(configuration);
            if (restrictedKey is null) return Results.Problem(statusCode: 503);
            if (await db.Plans.FindAsync([request.PlanId], ct) is null) return Results.NotFound();
            var options = StripeIntegrationOptions.Create(restrictedKey);
            var session = await provider.CreateCheckoutSessionAsync(request, options, ct);
            return session is null ? Results.Problem(statusCode: 503) : Results.Created($"/api/v1/subscriptions/payments/sessions/{session.Id}", new
            {
                session.Id,
                session.Url,
                sessionId = session.Id,
                checkoutUrl = session.Url,
                session.AmountInCents,
                options.UsesDynamicPaymentMethods
            });
        }).RequireAuthorization();
        subs.MapPatch("/subscriptions/payments/sessions/{sessionId}", async (string sessionId, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            var confirmation = await provider.ConfirmSessionAsync(sessionId, ct);
            if (confirmation is null) return Results.Problem(statusCode: 503);

            var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.BuilderId == confirmation.BuilderId && s.PlanId == confirmation.PlanId && s.Status == "active", ct);
            
            // Deactivate any other active subscriptions for this builder
            var otherActiveSubs = await db.Subscriptions
                .Where(s => s.BuilderId == confirmation.BuilderId && s.Status == "active" && (existing == null || s.Id != existing.Id))
                .ToListAsync(ct);
            foreach (var sub in otherActiveSubs)
            {
                sub.Status = "expired";
                sub.EndDate = DateTime.UtcNow;
            }

            if (existing is null)
            {
                db.Subscriptions.Add(new Subscription
                {
                    BuilderId = confirmation.BuilderId,
                    PlanId = confirmation.PlanId,
                    Status = "active",
                    StartDate = DateTime.UtcNow
                });
            }
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
            {
                // Lost a concurrent confirm race on the single-active arbiter.
                // The winner owns the active row; the loser reports conflict so
                // the client retries and converges on the winner.
                return Results.Conflict(new { error = "Subscription already active; refresh to see current state." });
            }

            return Results.Ok(confirmation);
        });
        subs.MapGet("/subscriptions/payments/invoices", async (int builderId, System.Security.Claims.ClaimsPrincipal user, IPaymentProvider provider, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!OwnsBuilder(user, builderId)) return Results.Forbid();
            var invoices = await provider.GetInvoicesAsync(builderId, ct);
            if (invoices is not null)
            {
                return Results.Ok(invoices);
            }

            // Fallback: If Stripe provider has no live key or customer configured,
            // synthesize invoices from the builder's actual database subscriptions.
            var builderSubs = await db.Subscriptions.Where(s => s.BuilderId == builderId).ToListAsync(ct);
            var plans = await db.Plans.ToDictionaryAsync(p => p.Id, ct);

            var fallbackInvoices = builderSubs.Select(s =>
            {
                var plan = plans.TryGetValue(s.PlanId, out var p) ? p : null;
                var amountInCents = plan is not null ? (long)(plan.Price * 100) : 29900L;
                return new
                {
                    Id = $"in_sub_{s.Id}",
                    Status = s.Status == "active" ? "paid" : s.Status,
                    AmountInCents = amountInCents,
                    Amount = plan?.Price ?? 299m,
                    Currency = "USD",
                    Description = $"Suscripción Plan {plan?.Name ?? "IoBuild"}",
                    Date = s.StartDate,
                    ReceiptUrl = (string?)null
                };
            }).ToList();

            return Results.Ok(fallbackInvoices);
        }).RequireAuthorization();
        subs.MapPost("/subscriptions", async (CreateSubscriptionRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!OwnsBuilder(user, request.BuilderId)) return Results.Forbid();
            var subscription = new Subscription { BuilderId = request.BuilderId, PlanId = request.PlanId, StartDate = request.StartDate, EndDate = request.EndDate };
            db.Subscriptions.Add(subscription);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/subscriptions/{subscription.Id}", subscription);
        }).RequireAuthorization();
        subs.MapPost("/webhooks/stripe", async (HttpRequest request, StripeWebhookProcessor processor, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync(ct);
            using var document = ParseWebhookPayload(payload);
            if (document is null) return Results.BadRequest();
            var eventId = document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
            var eventType = document.RootElement.TryGetProperty("type", out var type) ? type.GetString() : null;
            if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType)) return Results.BadRequest();
            var signature = request.Headers["Stripe-Signature"].ToString();
            return await processor.ProcessAsync(new StripeWebhookRequest(eventId, eventType, payload, signature), ct)
                ? Results.Ok(new { received = true, eventId })
                : Results.Unauthorized();
        }).AllowAnonymous();
    }
}
