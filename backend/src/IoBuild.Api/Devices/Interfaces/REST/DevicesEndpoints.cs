using System.Text.Json;
using IoBuild.Api.Devices.Application.Internal.CommandServices;
using IoBuild.Api.Devices.Domain.Model.Catalog;
using IoBuild.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Interfaces.REST;

public static class DevicesEndpoints
{
    // Mutations need a manager: the unit owner for unit devices, the project
    // builder for project (floor) devices. Anything else reads as not found.
    private static async Task<bool> ManagesDeviceAsync(System.Security.Claims.ClaimsPrincipal user, IoBuild.Api.Persistence.IoBuildDbContext db, IoBuild.Api.Devices.Domain.Model.Aggregates.Device device, CancellationToken ct)
    {
        if (!int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id)) return false;
        var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (device.UnitId.HasValue && string.Equals(role, "Owner", StringComparison.Ordinal)
            && await db.UnitOwnerProjections.AnyAsync(item => item.UnitId == device.UnitId && item.OwnerUserId == id, ct)) return true;
        var project = await db.Projects.FindAsync([device.ProjectId], ct);
        return project is not null && string.Equals(role, "Builder", StringComparison.OrdinalIgnoreCase) && project.BuilderId == id;
    }

    public static void MapDevicesEndpoints(this WebApplication app)
    {
        var deviceTypesResult = new
        {
            deviceTypes = new object[]
            {
                new { code = "SmartMeter", displayName = "Smart Meter", scope = "floor", controllableAttributes = Array.Empty<object>() },
                new { code = "WaterSensor", displayName = "Water Sensor", scope = "floor", controllableAttributes = Array.Empty<object>() },
                new { code = "SmokeDetector", displayName = "Smoke Detector", scope = "floor", controllableAttributes = Array.Empty<object>() },
                new { code = "AirConditioner", displayName = "Air Conditioner", scope = "unit", controllableAttributes = new object[] { new { name = "targetTemperature", type = "number", min = 16, max = 30, unit = "C", enumMembers = (string[]?)null }, new { name = "mode", type = "enum", min = (double?)null, max = (double?)null, unit = (string?)null, enumMembers = new[] { "cooling", "heating", "fan" } }, new { name = "power", type = "boolean", min = (double?)null, max = (double?)null, unit = (string?)null, enumMembers = (string[]?)null } } },
                new { code = "SmartLight", displayName = "Smart Light", scope = "unit", controllableAttributes = new object[] { new { name = "brightness", type = "number", min = 0, max = 100, unit = "%", enumMembers = (string[]?)null }, new { name = "power", type = "boolean", min = (double?)null, max = (double?)null, unit = (string?)null, enumMembers = (string[]?)null } } }
            }
        };

        var group = app.MapGroup("/api/v1").WithTags("Devices");

        group.MapGet("/devices/types", () => Results.Ok(deviceTypesResult)).AllowAnonymous();
        group.MapGet("/custom-device-types", () => Results.Ok(deviceTypesResult)).AllowAnonymous();
        group.MapGet("/devices", async ([FromQuery] int? unitId, [FromQuery] int? projectId, IoBuildDbContext db, CancellationToken ct) =>
        {
            var query = db.Devices.AsQueryable();
            if (unitId.HasValue) query = query.Where(d => d.UnitId == unitId.Value);
            if (projectId.HasValue) query = query.Where(d => d.ProjectId == projectId.Value);
            return Results.Ok((await query.OrderBy(device => device.Id).ToListAsync(ct)).Select(DeviceResponse.From));
        }).RequireAuthorization();
        group.MapGet("/devices/{id:int}", async (int id, IoBuildDbContext db, CancellationToken ct) => await db.Devices.FindAsync([id], ct) is { } device ? Results.Ok(DeviceResponse.From(device)) : Results.NotFound()).RequireAuthorization();
        group.MapPost("/devices", async (CreateDeviceRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, DeviceRegistryService registry, CancellationToken ct) =>
        {
            var ownerId = int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var id) ? id : 0;
            var isOwnerCustom = request.UnitId.HasValue;
            if (isOwnerCustom)
            {
                if (!string.Equals(user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, "Owner", StringComparison.Ordinal)) return Results.Json(new { error = "Only unit owners may add custom devices." }, statusCode: 403);
                if (!await db.UnitOwnerProjections.AnyAsync(item => item.UnitId == request.UnitId && item.OwnerUserId == ownerId, ct)) return Results.Json(new { error = $"You do not own unit {request.UnitId} or ownership has not yet propagated." }, statusCode: 403);
                var catalog = DeviceCatalog.Find(request.Type);
                if (catalog is null) return Results.BadRequest(new { error = $"Device type '{request.Type}' is not in the catalog. Please select a type from the available catalog." });
                if (catalog.Scope == "floor") return Results.BadRequest(new { error = $"Device type '{request.Type}' cannot be added to a unit. This type is designated for floor-level provisioning only." });
                if (await db.Devices.AnyAsync(item => item.ProjectId == request.ProjectId && item.UnitId == request.UnitId && item.Type == request.Type, ct)) return Results.Conflict(new { error = "A device of this type already exists in this unit." });
            }
            else if (!string.IsNullOrWhiteSpace(request.MacAddress) && await db.Devices.AnyAsync(item => item.MacAddress == request.MacAddress, ct)) return Results.Conflict(new { error = "A device with the same MAC address already exists." });
            var device = new Device { Name = request.Name, Type = request.Type, Location = request.Location, MacAddress = isOwnerCustom ? null : request.MacAddress, ProjectId = request.ProjectId, UnitId = request.UnitId, Source = isOwnerCustom ? "OwnerCustom" : null, Status = request.Status, OwnerId = ownerId };
            db.Devices.Add(device);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Conflict(new { error = isOwnerCustom ? "A device of this type already exists in this unit." : "A device with the same MAC address already exists." }); }
            db.DeviceProjections.Add(new IoBuild.Api.Analytics.Domain.Model.Aggregates.DeviceProjection
            {
                DeviceId = device.Id,
                ProjectId = device.ProjectId,
                UnitId = device.UnitId,
                DeviceName = device.Name,
                DeviceType = device.Type,
                Status = device.Status,
                OwnerUserId = ownerId,
                LastEventAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            await registry.AnnounceAsync(device, ct); return Results.Created($"/api/v1/devices/{device.Id}", DeviceResponse.From(device));
        }).RequireAuthorization();
        group.MapPut("/devices/{id:int}", async (int id, CreateDeviceRequest request, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, DeviceRegistryService registry, CancellationToken ct) => { var device = await db.Devices.FindAsync([id], ct); if (device is null || !await ManagesDeviceAsync(user, db, device, ct)) return Results.NotFound(); device.Name = request.Name; device.Type = request.Type; device.Location = request.Location; device.MacAddress = request.MacAddress; device.ProjectId = request.ProjectId; device.Status = request.Status; await db.SaveChangesAsync(ct); await registry.AnnounceAsync(device, ct); return Results.NoContent(); }).RequireAuthorization();
        group.MapDelete("/devices/{id:int}", async (int id, System.Security.Claims.ClaimsPrincipal user, IoBuildDbContext db, DeviceRegistryService registry, CancellationToken ct) => { var device = await db.Devices.FindAsync([id], ct); if (device is null || !await ManagesDeviceAsync(user, db, device, ct)) return Results.NotFound(); registry.QueueTombstone(id); var devProj = await db.DeviceProjections.FindAsync([id], ct); if (devProj is not null) db.DeviceProjections.Remove(devProj); db.Devices.Remove(device); await db.SaveChangesAsync(ct); try { await registry.ReconcileAsync(ct); } catch (HttpRequestException) { } return Results.NoContent(); }).RequireAuthorization();
        group.MapPost("/devices/{id:int}/commands", async (int id, DeviceCommandRequest request, System.Security.Claims.ClaimsPrincipal user, DeviceCommandService commands, CancellationToken ct) => { var ownerId = int.TryParse(user.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value, out var value) ? value : 0; var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value; try { var command = await commands.SendAuthorizedAsync(id, ownerId, role, request.Attribute, request.Value, ct); return Results.Ok(new { deviceId = id, attribute = request.Attribute, value = request.Value, acceptedAt = command.IssuedAt }); } catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); } catch (UnauthorizedAccessException exception) { return Results.Json(new { error = exception.Message }, statusCode: 403); } catch (KeyNotFoundException exception) { return Results.NotFound(new { error = exception.Message }); } catch (HttpRequestException exception) { return Results.Json(new { error = exception.Message }, statusCode: 503); } }).RequireAuthorization();
        group.MapPost("/devices/telemetry", async (TelemetryMessage request, DeviceTelemetryService telemetry, CancellationToken ct) => await telemetry.IngestAsync(request, ct) ? Results.Ok(new { received = true }) : Results.NotFound()).AllowAnonymous();
        group.MapPost("/devices/telemetry/replay", async (DeviceTelemetryService telemetry, CancellationToken ct) => Results.Ok(new { replayed = await telemetry.ReplayInfluxAsync(ct) })).RequireAuthorization();
        group.MapGet("/devices/{id:int}/energy", async (int id, DateTimeOffset? from, DateTimeOffset? to, IoBuildDbContext db, CancellationToken ct) =>
        {
            if (await db.Devices.FindAsync([id], ct) is null) return Results.NotFound(new { message = $"Device with ID {id} not found" });
            var start = from ?? DateTimeOffset.UtcNow.AddDays(-1); var end = to ?? DateTimeOffset.UtcNow;
            return Results.Ok(await db.DeviceTelemetry.Where(item => item.DeviceId == id && item.OccurredAt >= start && item.OccurredAt <= end).OrderBy(item => item.OccurredAt).Select(item => new { timestamp = item.OccurredAt, energyKwh = item.EnergyKwh, temperatureC = item.TemperatureC, voltageV = item.VoltageV }).ToListAsync(ct));
        }).RequireAuthorization();
        group.MapGet("/devices/{id:int}/status", async (int id, IoBuildDbContext db, CancellationToken ct) =>
        {
            var device = await db.Devices.FindAsync([id], ct);
            if (device is null) return Results.NotFound(new { message = $"Device with ID {id} not found" });
            var telemetry = await db.DeviceTelemetry.Where(item => item.DeviceId == id).OrderByDescending(item => item.OccurredAt).FirstOrDefaultAsync(ct);
            var shadow = await db.DeviceShadows.FindAsync([id], ct);
            object? desired = null;
            if (shadow?.DesiredJson is { Length: > 0 } desiredJson)
            {
                try { desired = JsonSerializer.Deserialize<JsonElement>(desiredJson); }
                catch { desired = null; }
            }
            var effectiveStatus = telemetry?.Status ?? device.Status ?? "online";
            var lastSeen = telemetry?.OccurredAt ?? DateTimeOffset.UtcNow;
            var tempC = telemetry?.TemperatureC ?? 22.0;
            var voltV = telemetry?.VoltageV ?? 220.0;
            return Results.Ok(new
            {
                deviceId = id,
                status = effectiveStatus,
                lastSeen,
                temperatureC = tempC,
                voltageV = voltV,
                desired
            });
        }).RequireAuthorization();
    }
}
