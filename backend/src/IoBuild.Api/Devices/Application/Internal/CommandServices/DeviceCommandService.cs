using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Catalog;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Devices.Infrastructure.Mqtt;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Application.Internal.CommandServices;

public sealed class DeviceCommandService(IoBuildDbContext db, IDeviceMqttPublisher mqtt)
{
    public Task<DeviceCommand> SendAsync(int deviceId, int requestingOwnerId, string attribute, string value, CancellationToken cancellationToken = default) =>
        SendAsync(deviceId, requestingOwnerId, attribute, JsonSerializer.SerializeToElement(value), cancellationToken);

    public async Task<DeviceCommand> SendAsync(int deviceId, int requestingOwnerId, string attribute, JsonElement value, CancellationToken cancellationToken = default)
    {
        using var lease = await DeviceStateLocks.EnterAsync(deviceId, cancellationToken);
        return await SendLockedAsync(deviceId, requestingOwnerId, attribute, value, cancellationToken);
    }

    public async Task<DeviceCommand> SendAuthorizedAsync(int deviceId, int requestingOwnerId, string? requestingRole, string attribute, JsonElement value, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(requestingRole, "Owner", StringComparison.Ordinal)) throw new UnauthorizedAccessException("Only unit owners may send device commands.");
        using var lease = await DeviceStateLocks.EnterAsync(deviceId, cancellationToken);
        var device = await db.Devices.FindAsync([deviceId], cancellationToken) ?? throw new KeyNotFoundException("Device not found.");
        if (!device.UnitId.HasValue) throw new UnauthorizedAccessException("Device is not assigned to a unit.");
        if (!await db.UnitOwnerProjections.AnyAsync(item => item.UnitId == device.UnitId && item.OwnerUserId == requestingOwnerId, cancellationToken)) throw new UnauthorizedAccessException("You do not own this unit or ownership has not yet propagated.");
        return await SendLockedAsync(deviceId, requestingOwnerId, attribute, value, cancellationToken);
    }

    private async Task<DeviceCommand> SendLockedAsync(int deviceId, int requestingOwnerId, string attribute, JsonElement value, CancellationToken cancellationToken)
    {
        var device = await db.Devices.FindAsync([deviceId], cancellationToken) ?? throw new KeyNotFoundException("Device not found.");
        if (device.OwnerId != requestingOwnerId)
        {
            if (device.UnitId.HasValue && await db.UnitOwnerProjections.AnyAsync(item => item.UnitId == device.UnitId.Value && item.OwnerUserId == requestingOwnerId, cancellationToken))
            {
                device.OwnerId = requestingOwnerId;
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw new UnauthorizedAccessException("Device owner is required.");
            }
        }
        ValidateCommand(device.Type, attribute, value);
        var shadow = await db.DeviceShadows.FindAsync([deviceId], cancellationToken);
        var desired = shadow?.DesiredJson is { Length: > 0 } json ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [] : [];
        if (attribute != "power" && desired.TryGetValue("power", out var power) && power.ValueKind == JsonValueKind.False)
            throw new InvalidOperationException("Device is powered off; turn it on before changing other attributes.");
        desired[attribute] = value;
        var desiredJson = JsonSerializer.Serialize(desired);
        if (shadow is null)
        {
            shadow = new DeviceShadow { DeviceId = deviceId, DesiredJson = desiredJson, ShadowVersion = 1, UpdatedAt = DateTimeOffset.UtcNow };
            db.DeviceShadows.Add(shadow);
        }
        else { shadow.DesiredJson = desiredJson; shadow.ShadowVersion++; shadow.UpdatedAt = DateTimeOffset.UtcNow; }
        var command = new DeviceCommand { DeviceId = deviceId, CommandId = Guid.NewGuid().ToString("N"), DesiredJson = desiredJson, IssuedAt = DateTimeOffset.UtcNow };
        db.DeviceCommands.Add(command);
        await db.SaveChangesAsync(cancellationToken);
        await PublishAndMarkAsync(command, cancellationToken);
        return command;
    }

    public async Task RepublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.DeviceCommands
            .Where(command => command.AcknowledgedAt == null)
            .OrderBy(command => command.DeviceId).ThenBy(command => command.IssuedAt).ThenBy(command => command.Id)
            .ToListAsync(cancellationToken);
        foreach (var command in pending) await PublishAndMarkAsync(command, cancellationToken);
    }

    private async Task PublishAndMarkAsync(DeviceCommand command, CancellationToken cancellationToken)
    {
        await mqtt.PublishAsync($"commands/{command.DeviceId}", command.DesiredJson, qos1: true, retain: true, cancellationToken);
        command.PublishAttempts++;
        command.PublishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCommand(string type, string attribute, JsonElement value)
    {
        var valid = type switch
        {
            "AirConditioner" => attribute is "power" or "mode" or "targetTemperature",
            "SmartLight" => attribute is "power" or "brightness",
            _ => false
        };
        if (!valid) throw new ArgumentException($"Attribute '{attribute}' is not valid for device type '{type}'.");
        if (attribute == "brightness" && (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var brightness) || brightness < 0 || brightness > 100)) throw new ArgumentException("brightness must be a number from 0 to 100.");
        if (attribute == "targetTemperature" && (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var temperature) || temperature < 16 || temperature > 30)) throw new ArgumentException("targetTemperature must be a number from 16 to 30.");
        if (attribute == "power" && value.ValueKind is not JsonValueKind.True and not JsonValueKind.False) throw new ArgumentException("power must be a boolean.");
        if (attribute == "mode" && (value.ValueKind != JsonValueKind.String || value.GetString() is not ("cooling" or "heating" or "fan"))) throw new ArgumentException("mode must be cooling, heating, or fan.");
    }
}
