using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Queries;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Analytics.Interfaces.REST;

public static class AnalyticsEndpoints
{
    // Dashboards serve only the caller: the JWT user id must equal the route
    // user id (403 on explicit mismatch, 401 anonymous). Insights are scoped
    // to projects the caller owns as builder or occupies as unit owner.
    private static int SelfId(System.Security.Claims.ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) ? id : 0;

    private static async Task<bool> MaySeeProjectAsync(System.Security.Claims.ClaimsPrincipal user, IoBuild.Api.Persistence.IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var self = SelfId(user);
        if (await db.Projects.AnyAsync(p => p.Id == projectId && p.BuilderId == self, ct)) return true;
        return await db.Units.AnyAsync(u => u.ProjectId == projectId && u.OwnerId == self, ct)
            || await db.UnitOwnerProjections.AnyAsync(p => p.OwnerUserId == self && db.Units.Any(u => u.Id == p.UnitId && u.ProjectId == projectId), ct);
    }

    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/analytics").WithTags("Analytics");

        group.MapGet("/builders/{userId:int}/metrics", async (int userId, System.Security.Claims.ClaimsPrincipal user, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetBuilderDashboardQuery(userId), ct);
            return result is null ? Results.NotFound(new { message = "No builder metrics found for the specified user." }) : Results.Ok(result);
        }).RequireAuthorization();
        group.MapGet("/owners/{userId:int}/metrics", async (int userId, System.Security.Claims.ClaimsPrincipal user, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetOwnerDashboardQuery(userId), ct);
            return result is null ? Results.NotFound(new { message = "No owner metrics found for the specified user." }) : Results.Ok(result);
        }).RequireAuthorization();
        group.MapGet("/builders/{userId:int}/energy", async (int userId, System.Security.Claims.ClaimsPrincipal user, int? minutes, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetBuilderLiveEnergyQuery(userId, Math.Clamp(minutes ?? 10, 1, 60)), ct);
            return Results.Ok(result.Select(point => new { timestamp = point.Timestamp, totalEnergyKwh = point.TotalEnergyKwh }));
        }).RequireAuthorization();
        group.MapGet("/owners/{userId:int}/energy", async (int userId, System.Security.Claims.ClaimsPrincipal user, int? minutes, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (SelfId(user) != userId) return Results.Forbid();
            var result = await analytics.Handle(new GetOwnerLiveEnergyQuery(userId, Math.Clamp(minutes ?? 10, 1, 60)), ct);
            return Results.Ok(result.Select(point => new { timestamp = point.Timestamp, totalEnergyKwh = point.TotalEnergyKwh }));
        }).RequireAuthorization();
        group.MapGet("/insights", async (int projectId, string? metric, DateTime? startDate, DateTime? endDate, System.Security.Claims.ClaimsPrincipal user, IoBuild.Api.Persistence.IoBuildDbContext db, IAnalyticsQueryService analytics, CancellationToken ct) =>
        {
            if (!await MaySeeProjectAsync(user, db, projectId, ct)) return Results.NotFound(new { message = "No insights found for the specified project." });
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;
            var result = await analytics.Handle(new GetHistoricalDataQuery(projectId, metric ?? "temperature", start, end), ct);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
