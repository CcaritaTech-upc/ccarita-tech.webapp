using System.Text.Json;
using System.Text.Json.Nodes;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Catalog;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Devices.Application.Internal.CommandServices;

public sealed record TelemetryMessage(int DeviceId, string EventId, DateTimeOffset OccurredAt, string Status, string ReportedJson, double EnergyKwh, double TemperatureC = 0, double VoltageV = 0);
public interface IInfluxTelemetrySink { Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default); }
public interface IDeviceMqttPublisher { Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default); }

public sealed class DisabledInfluxTelemetrySink : IInfluxTelemetrySink
{
    public Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default) => throw new HttpRequestException("InfluxDB is not configured.");
}

public sealed class DisabledDeviceMqttPublisher : IDeviceMqttPublisher
{
    public Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class DeviceTelemetryService(IoBuildDbContext db, IInfluxTelemetrySink influx)
{
    public async Task<bool> IngestAsync(TelemetryMessage message, CancellationToken cancellationToken = default)
    {
        using var lease = await DeviceStateLocks.EnterAsync(message.DeviceId, cancellationToken);
        return await IngestLockedAsync(message, cancellationToken);
    }

    private async Task<bool> IngestLockedAsync(TelemetryMessage message, CancellationToken cancellationToken)
    {
        var existing = await db.DeviceTelemetry.SingleOrDefaultAsync(item => item.EventId == message.EventId, cancellationToken);
        if (existing is not null)
        {
            if (existing.InfluxWrittenAt is null)
            {
                if (!await db.TelemetryRecoveries.AnyAsync(item => item.EventId == existing.EventId, cancellationToken))
                {
                    db.TelemetryRecoveries.Add(new TelemetryRecovery { EventId = existing.EventId, CreatedAt = DateTimeOffset.UtcNow });
                    await db.SaveChangesAsync(cancellationToken);
                }
                try { await ReplayExistingAsync(existing, cancellationToken); }
                catch (HttpRequestException) { }
            }
            return true;
        }
        var device = await db.Devices.FindAsync([message.DeviceId], cancellationToken);
        if (device is null) return false;
        device.Status = message.Status;
        var devProj = await db.DeviceProjections.FindAsync([message.DeviceId], cancellationToken);
        if (devProj is not null)
        {
            devProj.Status = message.Status;
            devProj.LastEventAt = message.OccurredAt.UtcDateTime;
        }
        var shadow = await db.DeviceShadows.FindAsync([message.DeviceId], cancellationToken);
        var isNewer = shadow is null || message.OccurredAt > shadow.ReportedAt;
        var record = new DeviceTelemetry { DeviceId = message.DeviceId, EventId = message.EventId, OccurredAt = message.OccurredAt, Status = message.Status, ReportedJson = message.ReportedJson, EnergyKwh = message.EnergyKwh, TemperatureC = message.TemperatureC, VoltageV = message.VoltageV };
        db.DeviceTelemetry.Add(record);
        if (isNewer)
        {
            if (shadow is null) { shadow = new DeviceShadow { DeviceId = message.DeviceId, ReportedJson = message.ReportedJson, ReportedAt = message.OccurredAt, UpdatedAt = DateTimeOffset.UtcNow }; db.DeviceShadows.Add(shadow); }
            else { shadow.ReportedJson = message.ReportedJson; shadow.ReportedAt = message.OccurredAt; }
        }
        var command = await db.DeviceCommands.SingleOrDefaultAsync(item => item.CommandId == message.EventId, cancellationToken);
        if (command is null)
        {
            var pending = await db.DeviceCommands
                .Where(item => item.DeviceId == message.DeviceId && item.AcknowledgedAt == null && item.IssuedAt <= message.OccurredAt)
                .OrderByDescending(item => item.IssuedAt)
                .ToListAsync(cancellationToken);
            command = pending.FirstOrDefault(item => EquivalentJson(item.DesiredJson, message.ReportedJson));
        }
        if (command is not null) command.AcknowledgedAt = message.OccurredAt;
        db.TelemetryRecoveries.Add(new TelemetryRecovery { EventId = message.EventId, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        try { await ReplayExistingAsync(record, cancellationToken); }
        catch (Exception) { }
        return true;
    }

    private async Task ReplayExistingAsync(DeviceTelemetry record, CancellationToken cancellationToken)
    {
        try
        {
            await influx.WriteAsync(new TelemetryMessage(record.DeviceId, record.EventId, record.OccurredAt, record.Status, record.ReportedJson, record.EnergyKwh, record.TemperatureC, record.VoltageV), cancellationToken);
            record.InfluxWrittenAt = DateTimeOffset.UtcNow;
            var intent = await db.TelemetryRecoveries.SingleOrDefaultAsync(item => item.EventId == record.EventId, cancellationToken);
            if (intent is not null) db.TelemetryRecoveries.Remove(intent);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Influx is auxiliary; failure must never break or crash telemetry ingestion.
        }
    }

    private static bool EquivalentJson(string expected, string actual)
    {
        try { return JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(actual)); }
        catch (JsonException) { return false; }
    }

    public async Task<int> ReplayInfluxAsync(CancellationToken cancellationToken = default)
    {
        var recoveries = await db.TelemetryRecoveries.OrderBy(item => item.Id).ToListAsync(cancellationToken); var replayed = 0;
        foreach (var recovery in recoveries)
        {
            var record = await db.DeviceTelemetry.SingleAsync(item => item.EventId == recovery.EventId, cancellationToken);
            try { await ReplayExistingAsync(record, cancellationToken); replayed++; }
            catch (Exception) { break; }
        }
        return replayed;
    }
}
