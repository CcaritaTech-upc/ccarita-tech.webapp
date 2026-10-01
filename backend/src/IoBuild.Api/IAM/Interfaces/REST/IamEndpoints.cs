using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace IoBuild.Api.IAM.Interfaces.REST;

/// <summary>
/// IAM endpoints. Extracted from Program.cs for readability (pure move, no behavior change).
/// </summary>
public static class IamEndpoints
{
    private static bool HasRole(ClaimsPrincipal user, string role) =>
        string.Equals(user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value, role, StringComparison.OrdinalIgnoreCase);

    public static void MapIamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").WithTags("IAM");

        group.MapPost("/users", async (RegisterUser request, IamService iam, IoBuildDbContext db, CancellationToken ct) =>
        {
            var rawEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!string.IsNullOrEmpty(rawEmail) && await db.IamUsers.AnyAsync(u => u.Email == rawEmail, ct))
            {
                return Results.Conflict(new { error = "An account with this email already exists." });
            }
            try { await iam.RegisterAsync(request, ct); return Results.Created("/api/v1/users", new { message = "User created successfully." }); }
            catch (OwnerUnitAssignmentRequiredException)
            {
                return Results.Json(new { code = "owner_unit_assignment_required", error = "An assigned unit is required to register as an owner." }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException) { return Results.BadRequest(new { error = "Invalid registration data." }); }
        }).AllowAnonymous();

        group.MapPost("/authentication/sign-up", async (RegisterUser request, IamService iam, IoBuildDbContext db, CancellationToken ct) =>
        {
            var rawEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!string.IsNullOrEmpty(rawEmail) && await db.IamUsers.AnyAsync(u => u.Email == rawEmail, ct))
            {
                return Results.Conflict(new { error = "An account with this email already exists." });
            }
            try { await iam.RegisterAsync(request, ct); return Results.Created("/api/v1/authentication/sign-up", new { message = "User created successfully." }); }
            catch (OwnerUnitAssignmentRequiredException)
            {
                return Results.Json(new { code = "owner_unit_assignment_required", error = "An assigned unit is required to register as an owner." }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (InvalidOperationException) { return Results.BadRequest(new { error = "Invalid registration data." }); }
        }).AllowAnonymous();

        group.MapGet("/authentication/invitation", async (string? email, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email)) return Results.Ok(new { assigned = false, alreadyRegistered = false });
            var normalized = email.Trim().ToLowerInvariant();

            var alreadyRegistered = await db.IamUsers.AnyAsync(u => u.Email == normalized, ct);
            if (alreadyRegistered)
            {
                return Results.Ok(new { assigned = false, alreadyRegistered = true });
            }

            var matchingClientUnitIds = db.Clients
                .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == normalized && c.UnitId.HasValue)
                .Select(c => c.UnitId!.Value);
            var hasAssignedUnit = await db.Units.AnyAsync(u =>
                (!string.IsNullOrEmpty(u.OwnerEmail) && u.OwnerEmail.ToLower() == normalized) || matchingClientUnitIds.Contains(u.Id), ct);

            return Results.Ok(new
            {
                assigned = hasAssignedUnit,
                alreadyRegistered = false,
            });
        }).AllowAnonymous();

        group.MapPost("/sessions", async (SignIn request, IamService iam, CancellationToken ct) =>
        {
            try { return Results.Created("/api/v1/sessions", await iam.SignInAsync(request, ct)); }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        }).AllowAnonymous();
        group.MapPost("/authentication/sign-in", async (SignIn request, IamService iam, CancellationToken ct) =>
        {
            try { return Results.Created("/api/v1/authentication/sign-in", await iam.SignInAsync(request, ct)); }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        }).AllowAnonymous();

        group.MapDelete("/sessions/current", async (HttpRequest request, IamService iam, CancellationToken ct) =>
        {
            var header = request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "No token provided." });
            await iam.RevokeAsync(header["Bearer ".Length..].Trim(), ct);
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapPost("/authentication/sign-out", async (HttpRequest request, IamService iam, CancellationToken ct) =>
        {
            var header = request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "No token provided." });
            await iam.RevokeAsync(header["Bearer ".Length..].Trim(), ct);
            return Results.Ok(new { message = "Signed out successfully." });
        }).RequireAuthorization();

        group.MapGet("/users", async (ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            // Public registration serves Builder and Owner only. The global user directory is administrative.
            if (!HasRole(user, "Admin")) return Results.Forbid();
            return Results.Ok(await db.IamUsers.OrderBy(item => item.Id).Select(item => new { item.Id, item.Email, item.Role }).ToListAsync(ct));
        }).RequireAuthorization();
    }
}
