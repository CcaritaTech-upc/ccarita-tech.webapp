using IoBuild.Api.IAM.Application.Internal.CommandServices;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.IAM.Interfaces.REST;

/// <summary>
/// IAM endpoints. Extracted from Program.cs for readability (pure move, no behavior change).
/// </summary>
public static class IamEndpoints
{
    public static void MapIamEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").WithTags("IAM");

        group.MapPost("/users", async (RegisterUser request, IamService iam, CancellationToken ct) => { try { await iam.RegisterAsync(request, ct); return Results.Created("/api/v1/users", new { message = "User created successfully." }); } catch (InvalidOperationException) { return Results.BadRequest(new { error = "Invalid registration data." }); } }).AllowAnonymous();
        group.MapPost("/authentication/sign-up", async (RegisterUser request, IamService iam, CancellationToken ct) => { try { await iam.RegisterAsync(request, ct); return Results.Created("/api/v1/authentication/sign-up", new { message = "User created successfully." }); } catch (InvalidOperationException) { return Results.BadRequest(new { error = "Invalid registration data." }); } }).AllowAnonymous();

        group.MapGet("/authentication/invitation", async (string? email, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(email)) return Results.Ok(new { assigned = false });
            var normalized = email.Trim().ToLowerInvariant();

            var client = await db.Clients
                .FirstOrDefaultAsync(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == normalized, ct);

            var unit = await db.Units
                .FirstOrDefaultAsync(u => (!string.IsNullOrEmpty(u.OwnerEmail) && u.OwnerEmail.ToLower() == normalized) || (client != null && client.UnitId == u.Id), ct);

            if (client is null && unit is null)
            {
                return Results.Ok(new { assigned = false });
            }

            var project = unit is not null
                ? await db.Projects.FindAsync([unit.ProjectId], ct)
                : client is not null ? await db.Projects.FindAsync([client.ProjectId], ct) : null;

            return Results.Ok(new
            {
                assigned = true,
                fullName = client?.FullName ?? string.Empty,
                phoneNumber = client?.PhoneNumber ?? string.Empty,
                address = client?.Address ?? string.Empty,
                unitNumber = unit?.UnitNumber ?? client?.UnitNumber ?? string.Empty,
                projectName = project?.Name ?? client?.ProjectName ?? string.Empty,
                unitId = unit?.Id ?? client?.UnitId
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

        group.MapGet("/users", async (IoBuildDbContext db, CancellationToken ct) => Results.Ok(await db.IamUsers.OrderBy(user => user.Id).Select(user => new { user.Id, user.Email, user.Role }).ToListAsync(ct))).RequireAuthorization();
    }
}
