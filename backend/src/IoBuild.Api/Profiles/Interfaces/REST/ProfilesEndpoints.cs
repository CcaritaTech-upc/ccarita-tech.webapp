using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Persistence;
using IoBuild.Api.Profiles.Application.Internal.CommandServices;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Profiles.Interfaces.REST;

public static class ProfilesEndpoints
{
    // Same ownership convention as subscriptions: the JWT user id (Sid claim)
    // must equal the acted-upon user. Foreign ids read as not found so their
    // existence is never oracle-able; explicit mismatches are forbidden.
    private static int SelfId(System.Security.Claims.ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) ? id : 0;

    private static bool OwnsUser(System.Security.Claims.ClaimsPrincipal user, int userId) => SelfId(user) == userId;

    public static void MapProfilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/profiles").WithTags("Profiles");

        group.MapGet("", async (int? userId, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (userId.HasValue && !OwnsUser(user, userId.Value)) return Results.Forbid();
            var self = SelfId(user);
            return Results.Ok(await db.Profiles.Where(profile => profile.UserId == self).OrderBy(profile => profile.Id).ToListAsync(ct));
        }).RequireAuthorization();

        group.MapGet("/{id:int}", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Profiles.FindAsync([id], ct);
            return item is not null && OwnsUser(user, item.UserId) ? Results.Ok(item) : Results.NotFound();
        }).RequireAuthorization();

        group.MapPut("/{id:int}", async (int id, CreateProfileRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Profiles.FindAsync([id], ct);
            if (item is null || !OwnsUser(user, item.UserId)) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(request.Name)) item.Name = request.Name;
            if (!string.IsNullOrWhiteSpace(request.Username)) item.Username = request.Username;
            item.PhoneNumber = request.PhoneNumber;
            item.Address = request.Address;
            item.SecondEmail = request.SecondEmail;
            item.Age = request.Age;
            if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
            {
                item.PhotoUrl = request.PhotoUrl;
                item.CloudinaryReference = request.PhotoUrl;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(item);
        }).RequireAuthorization();

        group.MapPatch("/{userId:int}/photo", async (int userId, ReplaceProfilePhotoRequest request, System.Security.Claims.ClaimsPrincipal user, ProfilePhotoWorkflow workflow, CancellationToken ct) =>
            !OwnsUser(user, userId) ? Results.Forbid() : await workflow.ReplaceAsync(userId, request.ExpectedReference, request.Content, ct) ? Results.NoContent() : Results.Conflict()).RequireAuthorization();

        group.MapPost("", async (CreateProfileRequest request, System.Security.Claims.ClaimsPrincipal user, CoreBusinessService service, CancellationToken ct) =>
        {
            if (!OwnsUser(user, request.UserId)) return Results.Forbid();
            try
            {
                var profile = await service.CreateProfileAsync(
                    request.UserId,
                    request.Name,
                    request.Username,
                    request.PhoneNumber,
                    request.Address,
                    request.SecondEmail,
                    request.Age,
                    request.PhotoUrl,
                    ct);
                return Results.Created($"/api/v1/profiles/{profile.Id}", profile);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
            {
                // A profile already exists for this user (unique UserId index):
                // update it instead of duplicating.
                return Results.Conflict(new { error = "Profile already exists for this user." });
            }
        }).RequireAuthorization();
    }
}
