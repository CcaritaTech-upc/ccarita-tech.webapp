using System.Security.Claims;
using IoBuild.Api.CoreBusiness;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Domain.Services.Commands;
using IoBuild.Api.Publishing.Domain.Services.Queries;
using IoBuild.Api.Publishing.Interfaces.REST.Resources;
using IoBuild.Api.Publishing.Interfaces.REST.Transform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Interfaces.REST;

public static class PublishingEndpoints
{
    // Ownership convention: the JWT user id (Sid claim) must own the acted-upon
    // project, directly or through the unit/client parent. Foreign ids read as
    // not found; explicit mismatches are forbidden.
    private static int SelfId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value, out var id) ? id : 0;

    private static bool OwnsProject(ClaimsPrincipal user, IoBuild.Api.Publishing.Domain.Model.Aggregates.Project project) => SelfId(user) == project.BuilderId;

    private static async Task<bool> OwnsProjectIdAsync(ClaimsPrincipal user, IoBuildDbContext db, int projectId, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct);
        return project is not null && OwnsProject(user, project);
    }

    private static async Task<bool> OwnsUnitIdAsync(ClaimsPrincipal user, IoBuildDbContext db, int unitId, CancellationToken ct)
    {
        var unit = await db.Units.FindAsync([unitId], ct);
        return unit is not null && await OwnsProjectIdAsync(user, db, unit.ProjectId, ct);
    }

    public static void MapPublishingEndpoints(this WebApplication app)
    {
        // ── Projects Endpoints ──
        var projects = app.MapGroup("/api/v1/projects").WithTags("Projects");

        projects.MapGet("", async (ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var sid = user.FindFirst(ClaimTypes.Sid)?.Value ?? user.FindFirst("sid")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            var builderId = int.TryParse(sid, out var id) ? id : 0;
            var projectList = await db.Projects.Where(project => project.BuilderId == builderId).OrderBy(project => project.Id).ToListAsync(ct);
            var projectIds = projectList.Select(p => p.Id).ToList();
            var occupiedCounts = await db.Units
                .Where(u => projectIds.Contains(u.ProjectId) && (!string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue))
                .GroupBy(u => u.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.ProjectId, g => g.Count, ct);

            var result = projectList.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Location,
                p.TotalUnits,
                OccupiedUnits = occupiedCounts.GetValueOrDefault(p.Id, 0),
                p.BuilderId,
                p.ImageUrl,
                p.StructureDefined,
                p.CreatedAt
            });

            return Results.Ok(result);
        }).RequireAuthorization();

        projects.MapPost("", async (CreateProjectRequest request, ClaimsPrincipal user, CoreBusinessService service, CancellationToken ct) =>
        {
            var tokenBuilderId = SelfId(user);
            if (request.BuilderId.HasValue && request.BuilderId.Value > 0 && request.BuilderId.Value != tokenBuilderId) return Results.Forbid();
            var builderId = request.BuilderId.HasValue && request.BuilderId.Value > 0 ? request.BuilderId.Value : tokenBuilderId;
            var project = await service.CreateProjectAsync(request.Name, request.Description, request.Location, request.TotalUnits, builderId, request.ImageUrl, ct);
            return Results.Created($"/api/v1/projects/{project.Id}", project);
        }).RequireAuthorization();

        projects.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Projects.FindAsync([id], ct);
            if (item is null || !OwnsProject(user, item)) return Results.NotFound();
            var occupiedUnits = await db.Units
                .CountAsync(u => u.ProjectId == id && (!string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue), ct);
            return Results.Ok(new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Location,
                item.TotalUnits,
                OccupiedUnits = occupiedUnits,
                item.BuilderId,
                item.ImageUrl,
                item.StructureDefined,
                item.CreatedAt
            });
        }).RequireAuthorization();

        projects.MapPut("/{id:int}", async (int id, CreateProjectRequest request, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Projects.FindAsync([id], ct);
            if (item is null || !OwnsProject(user, item)) return Results.NotFound();
            item.Name = request.Name;
            item.Description = request.Description;
            item.Location = request.Location;
            item.TotalUnits = request.TotalUnits;
            item.ImageUrl = request.ImageUrl;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        projects.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IoBuildDbContext db, CancellationToken ct) =>
        {
            var item = await db.Projects.FindAsync([id], ct);
            if (item is null || !OwnsProject(user, item)) return Results.NotFound();
            db.Projects.Remove(item);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        projects.MapPost("/{id:int}/structure", async (int id, ProjectStructureRequest request, ClaimsPrincipal user, IProjectCommandService commandService, IoBuildDbContext db, CancellationToken ct) =>
        {
            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
            if (!string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { error = "Only users with the Builder role may define project structure." }, statusCode: 403);
            if (request.Floors < 1 || request.UnitsPerFloor < 1)
                return Results.Json(new { error = "floors and unitsPerFloor must be at least 1." }, statusCode: 422);
            if (request.FloorNumbers?.Any(floor => floor < 1 || floor > request.Floors) == true)
                return Results.BadRequest(new { error = "floor reference is out of range." });

            var project = await db.Projects.FindAsync([id], ct);
            if (project is null || !OwnsProject(user, project)) return Results.NotFound();
            if (project.StructureDefined) return Results.Conflict(new { error = "Project structure already defined." });

            await commandService.DefineProjectStructureAsync(id, request.Floors, request.UnitsPerFloor, request.FloorNumbers, ct);

            return Results.Created($"/api/v1/projects/{id}/structure", new { message = $"Project structure defined: {request.Floors} floor(s), {request.UnitsPerFloor} unit(s) per floor." });
        }).RequireAuthorization();

        // ── Units Endpoints ──
        var units = app.MapGroup("/api/v1/units").WithTags("Units");

        units.MapGet("", async ([FromQuery] int? projectId, [FromQuery] int? ownerId, IUnitQueryService queryService, CancellationToken ct) =>
        {
            IEnumerable<Unit> unitList;
            if (projectId.HasValue)
            {
                unitList = await queryService.Handle(new GetUnitsByProjectIdQuery(projectId.Value), ct);
            }
            else if (ownerId.HasValue)
            {
                unitList = await queryService.Handle(new GetUnitsByOwnerIdQuery(ownerId.Value), ct);
            }
            else
            {
                unitList = await queryService.Handle(new GetAllUnitsQuery(), ct);
            }
            return Results.Ok(unitList.Select(UnitResourceFromEntityAssembler.ToResourceFromEntity));
        }).RequireAuthorization();

        units.MapGet("/{id:int}", async (int id, IUnitQueryService queryService, CancellationToken ct) =>
        {
            var unit = await queryService.Handle(new GetUnitByIdQuery(id), ct);
            return unit is null ? Results.NotFound() : Results.Ok(UnitResourceFromEntityAssembler.ToResourceFromEntity(unit));
        }).RequireAuthorization();

        units.MapPost("", async (CreateUnitResource resource, ClaimsPrincipal user, IUnitCommandService commandService, IUnitQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsProjectIdAsync(user, db, resource.ProjectId, ct)) return Results.NotFound();
            var command = new CreateUnitCommand(resource.ProjectId, resource.UnitNumber, resource.OwnerId, resource.Floor, resource.RoomNumber);
            var unitId = await commandService.Handle(command, ct);
            var created = await queryService.Handle(new GetUnitByIdQuery(unitId), ct);
            return created is null ? Results.Problem(statusCode: 500) : Results.Created($"/api/v1/units/{unitId}", UnitResourceFromEntityAssembler.ToResourceFromEntity(created));
        }).RequireAuthorization();

        units.MapPatch("/{id:int}/assign-owner", async (int id, AssignUnitOwnerResource resource, ClaimsPrincipal user, IUnitCommandService commandService, IUnitQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsUnitIdAsync(user, db, id, ct)) return Results.NotFound();
            try
            {
                await commandService.Handle(new AssignUnitOwnerEmailCommand(id, resource.OwnerEmail, resource.OwnerId), ct);
                var updated = await queryService.Handle(new GetUnitByIdQuery(id), ct);
                return updated is null ? Results.NotFound() : Results.Ok(UnitResourceFromEntityAssembler.ToResourceFromEntity(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();

        units.MapPatch("/{id:int}", async (int id, AssignUnitOwnerResource resource, ClaimsPrincipal user, IUnitCommandService commandService, IUnitQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsUnitIdAsync(user, db, id, ct)) return Results.NotFound();
            try
            {
                await commandService.Handle(new AssignUnitOwnerEmailCommand(id, resource.OwnerEmail, resource.OwnerId), ct);
                var updated = await queryService.Handle(new GetUnitByIdQuery(id), ct);
                return updated is null ? Results.NotFound() : Results.Ok(UnitResourceFromEntityAssembler.ToResourceFromEntity(updated));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();

        // ── Clients Endpoints ──
        var clients = app.MapGroup("/api/v1/clients").WithTags("Clients");

        clients.MapGet("", async ([FromQuery] int? builderId, [FromQuery] int? projectId, IClientQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            IEnumerable<Client> clientList;
            if (builderId.HasValue)
            {
                clientList = await queryService.Handle(new GetClientsByBuilderIdQuery(builderId.Value), ct);
            }
            else if (projectId.HasValue)
            {
                clientList = await queryService.Handle(new GetClientsByProjectIdQuery(projectId.Value), ct);
            }
            else
            {
                clientList = await queryService.Handle(new GetAllClientsQuery(), ct);
            }

            var clientsArray = clientList.ToList();
            var unitIds = clientsArray.Where(c => c.UnitId.HasValue).Select(c => c.UnitId!.Value).Distinct().ToList();
            var deviceCounts = unitIds.Count > 0
                ? await db.Devices
                    .Where(d => d.UnitId.HasValue && unitIds.Contains(d.UnitId.Value))
                    .GroupBy(d => d.UnitId!.Value)
                    .Select(g => new { UnitId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.UnitId, g => g.Count, ct)
                : new Dictionary<int, int>();

            var result = clientsArray.Select(c =>
            {
                var count = c.UnitId.HasValue && deviceCounts.TryGetValue(c.UnitId.Value, out var val) ? val : 0;
                return ClientResourceFromEntityAssembler.ToResourceFromEntity(c, count);
            });

            return Results.Ok(result);
        }).RequireAuthorization();

        clients.MapGet("/{id:int}", async (int id, ClaimsPrincipal user, IClientQueryService queryService, IoBuildDbContext db, CancellationToken ct) =>
        {
            var client = await queryService.Handle(new GetClientByIdQuery(id), ct);
            if (client is null || client.BuilderId != SelfId(user)) return Results.NotFound();
            var deviceCount = client.UnitId.HasValue
                ? await db.Devices.CountAsync(d => d.UnitId == client.UnitId.Value, ct)
                : 0;
            return Results.Ok(ClientResourceFromEntityAssembler.ToResourceFromEntity(client, deviceCount));
        }).RequireAuthorization();

        clients.MapPost("", async (CreateClientResource resource, ClaimsPrincipal user, IClientCommandService commandService, IClientQueryService queryService, CancellationToken ct) =>
        {
            if (resource.BuilderId != SelfId(user)) return Results.Forbid();
            var command = new CreateClientCommand(
                resource.FullName,
                resource.ProjectName,
                resource.AccountStatement,
                resource.BuilderId,
                resource.ProjectId,
                resource.Email,
                resource.PhoneNumber,
                resource.Address,
                resource.UnitId,
                resource.UnitNumber);
            var clientId = await commandService.Handle(command, ct);
            var created = await queryService.Handle(new GetClientByIdQuery(clientId), ct);
            return created is null ? Results.Problem(statusCode: 500) : Results.Created($"/api/v1/clients/{clientId}", ClientResourceFromEntityAssembler.ToResourceFromEntity(created));
        }).RequireAuthorization();

        clients.MapPut("/{id:int}", async (int id, UpdateClientResource resource, ClaimsPrincipal user, IClientCommandService commandService, IClientQueryService queryService, CancellationToken ct) =>
        {
            var existingPut = await queryService.Handle(new GetClientByIdQuery(id), ct);
            if (existingPut is null || existingPut.BuilderId != SelfId(user)) return Results.NotFound();
            try
            {
                var command = new UpdateClientCommand(
                    id,
                    resource.FullName,
                    resource.ProjectName,
                    resource.AccountStatement,
                    resource.BuilderId,
                    resource.ProjectId,
                    resource.Email,
                    resource.PhoneNumber,
                    resource.Address,
                    resource.UnitId,
                    resource.UnitNumber);
                await commandService.Handle(command, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();

        clients.MapDelete("/{id:int}", async (int id, ClaimsPrincipal user, IClientCommandService commandService, IClientQueryService queryService, CancellationToken ct) =>
        {
            var existingDel = await queryService.Handle(new GetClientByIdQuery(id), ct);
            if (existingDel is null || existingDel.BuilderId != SelfId(user)) return Results.NotFound();
            try
            {
                await commandService.Handle(new DeleteClientCommand(id), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization();
    }
}
