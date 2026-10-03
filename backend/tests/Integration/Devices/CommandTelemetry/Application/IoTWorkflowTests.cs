using IoBuild.Api.Devices;
using IoBuild.Api.Devices.Application.Internal.CommandServices;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.Devices.Infrastructure.InfluxDB;
using IoBuild.Api.Devices.Infrastructure.Mqtt;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IoBuild.Integration.Tests.Devices.CommandTelemetry.Application;

[Trait("Context", "Devices")]
[Trait("Capability", "CommandTelemetry")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class IoTWorkflowTests
{
    [Fact]
    [Trait("Category", "IoT")]
    public async Task Telemetry_duplicate_and_stale_shadow_are_no_ops_but_newer_report_is_applied_once()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 7, Name = "AC", Type = "AirConditioner", Location = "A", ProjectId = 1, Status = "online", OwnerId = 1 });
        await db.SaveChangesAsync();
        var sink = new RecordingInfluxSink();
        var service = new DeviceTelemetryService(db, sink);
        var newer = new TelemetryMessage(7, "evt-2", DateTimeOffset.Parse("2026-08-30T00:02:00Z"), "online", "{\"power\":true}", 1.2);

        Assert.True(await service.IngestAsync(newer));
        Assert.True(await service.IngestAsync(newer));
        Assert.True(await service.IngestAsync(new TelemetryMessage(7, "evt-1", DateTimeOffset.Parse("2026-08-30T00:01:00Z"), "idle", "{\"power\":false}", 0.4)));

        Assert.Equal(2, sink.Writes.Count);
        var shadow = await db.DeviceShadows.SingleAsync();
        Assert.Equal("{\"power\":true}", shadow.ReportedJson);
        Assert.Equal(DateTimeOffset.Parse("2026-08-30T00:02:00Z"), shadow.ReportedAt);
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Influx_outage_is_durable_and_replay_writes_exactly_once()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 8, Name = "Meter", Type = "SmartMeter", Location = "B", ProjectId = 1, Status = "online", OwnerId = 1 });
        await db.SaveChangesAsync();
        var sink = new RecordingInfluxSink { IsAvailable = false };
        var service = new DeviceTelemetryService(db, sink);
        var message = new TelemetryMessage(8, "evt-outage", DateTimeOffset.Parse("2026-08-30T00:03:00Z"), "online", "{}", 2.0);

        Assert.True(await service.IngestAsync(message));
        Assert.Single(await db.TelemetryRecoveries.ToListAsync());
        sink.IsAvailable = true;
        Assert.Equal(1, await service.ReplayInfluxAsync());
        Assert.Equal(0, await service.ReplayInfluxAsync());
        Assert.Single(sink.Writes);
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Stale_telemetry_is_still_written_to_influx_without_regressing_the_reported_shadow()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 12, Name = "Meter", Type = "SmartMeter", Location = "D", ProjectId = 1, Status = "online", OwnerId = 1 });
        await db.SaveChangesAsync();
        var sink = new RecordingInfluxSink();
        var service = new DeviceTelemetryService(db, sink);

        await service.IngestAsync(new TelemetryMessage(12, "evt-new", DateTimeOffset.Parse("2026-08-30T00:05:00Z"), "online", "{\"power\":true}", 3.0));
        await service.IngestAsync(new TelemetryMessage(12, "evt-stale", DateTimeOffset.Parse("2026-08-30T00:04:00Z"), "idle", "{\"power\":false}", 2.0));

        Assert.Equal(new[] { "evt-new", "evt-stale" }, sink.Writes.Select(message => message.EventId));
        var shadow = await db.DeviceShadows.SingleAsync();
        Assert.Equal("{\"power\":true}", shadow.ReportedJson);
        Assert.Equal(DateTimeOffset.Parse("2026-08-30T00:05:00Z"), shadow.ReportedAt);
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Existing_unwritten_event_repairs_its_durable_influx_intent_on_retransmission()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 13, Name = "Probe", Type = "SmartMeter", Location = "E", ProjectId = 1, Status = "online", OwnerId = 1 });
        await db.SaveChangesAsync();
        var sink = new RecordingInfluxSink { IsAvailable = false };
        var service = new DeviceTelemetryService(db, sink);
        var message = new TelemetryMessage(13, "evt-repair", DateTimeOffset.Parse("2026-08-30T00:06:00Z"), "online", "{}", 1.1);

        Assert.True(await service.IngestAsync(message));
        Assert.Single(await db.TelemetryRecoveries.ToListAsync());
        sink.IsAvailable = true;
        Assert.True(await service.IngestAsync(message));

        Assert.Single(sink.Writes);
        Assert.Empty(await db.TelemetryRecoveries.ToListAsync());
        Assert.NotNull((await db.DeviceTelemetry.SingleAsync()).InfluxWrittenAt);
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Owner_command_publishes_retained_qos1_payload_and_ack_updates_reported_shadow()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 9, Name = "Light", Type = "SmartLight", Location = "C", ProjectId = 1, Status = "online", OwnerId = 4 });
        await db.SaveChangesAsync();
        var mqtt = new RecordingMqttPublisher();
        var commands = new DeviceCommandService(db, mqtt);

        var command = await commands.SendAsync(9, 4, "brightness", JsonSerializer.SerializeToElement(80));
        Assert.Equal("commands/9", mqtt.Topic);
        Assert.True(mqtt.QoS1 && mqtt.Retain);
        Assert.Contains("\"brightness\":80", mqtt.Payload);
        var telemetry = new DeviceTelemetryService(db, new RecordingInfluxSink());
        await telemetry.IngestAsync(new TelemetryMessage(9, command.CommandId, command.IssuedAt, "online", "{\"brightness\":80}", 0.1));
        Assert.True((await db.DeviceCommands.SingleAsync()).AcknowledgedAt.HasValue);
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Command_rejects_an_attribute_that_is_not_in_the_device_catalog()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 14, Name = "Sensor", Type = "SmartMeter", Location = "F", ProjectId = 1, Status = "online", OwnerId = 4 });
        await db.SaveChangesAsync();
        var commands = new DeviceCommandService(db, new RecordingMqttPublisher());

        await Assert.ThrowsAsync<ArgumentException>(() => commands.SendAsync(14, 4, "brightness", JsonSerializer.SerializeToElement(80)));
        Assert.Empty(await db.DeviceCommands.ToListAsync());
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Legacy_simulator_telemetry_acknowledges_the_matching_pending_command_without_a_command_event_id()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 10, Name = "Light", Type = "SmartLight", Location = "C", ProjectId = 1, Status = "online", OwnerId = 4 });
        await db.SaveChangesAsync();
        var commands = new DeviceCommandService(db, new RecordingMqttPublisher());
        var command = await commands.SendAsync(10, 4, "brightness", JsonSerializer.SerializeToElement(80));
        var telemetry = new DeviceTelemetryService(db, new RecordingInfluxSink());

        // The Python simulator's json.dumps formatting includes this whitespace.
        await telemetry.IngestAsync(new TelemetryMessage(10, "simulator-tick", command.IssuedAt.AddSeconds(1), "online", "{\"brightness\": 80}", 0.1));

        Assert.Equal(command.IssuedAt.AddSeconds(1), (await db.DeviceCommands.SingleAsync()).AcknowledgedAt);
    }

    private static IoBuildDbContext Db() => new(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class RecordingInfluxSink : IInfluxTelemetrySink
    {
        public bool IsAvailable { get; set; } = true;
        public List<TelemetryMessage> Writes { get; } = [];
        public Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable) throw new HttpRequestException("influx unavailable");
            Writes.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMqttPublisher : IDeviceMqttPublisher
    {
        public string Topic { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;
        public bool QoS1 { get; private set; }
        public bool Retain { get; private set; }
        public Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default)
        { Topic = topic; Payload = payload; QoS1 = qos1; Retain = retain; return Task.CompletedTask; }
    }
}

[Trait("Context", "Devices")]
[Trait("Capability", "CommandTelemetry")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class IoTWorkflowRemainingBlockerTests
{
    [Fact]
    [Trait("Category", "IoT")]
    public async Task Pending_command_is_durable_before_publish_and_reconciliation_republishes_only_unacknowledged_commands()
    {
        await using var db = TestDb();
        db.Devices.Add(new Device { Id = 20, Name = "Light", Type = "SmartLight", Location = "G", ProjectId = 1, Status = "online", OwnerId = 7 });
        await db.SaveChangesAsync();
        var mqtt = new RecordingPublisher();
        var commands = new DeviceCommandService(db, mqtt);

        var command = await commands.SendAsync(20, 7, "brightness", JsonSerializer.SerializeToElement(60));
        Assert.NotNull(command.PublishedAt);
        Assert.Equal(1, command.PublishAttempts);
        command.AcknowledgedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var pending = new DeviceCommand { DeviceId = 20, CommandId = "pending-replay", DesiredJson = "{\"brightness\":70}", IssuedAt = DateTimeOffset.UtcNow };
        db.DeviceCommands.Add(pending);
        await db.SaveChangesAsync();
        await commands.RepublishPendingAsync();

        Assert.Equal(new[] { "commands/20" }, mqtt.Topics.Skip(1));
        Assert.NotNull(pending.PublishedAt);
        Assert.Equal(1, pending.PublishAttempts);
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Duplicate_with_missing_recovery_intent_repairs_intent_before_replaying_influx()
    {
        await using var db = TestDb();
        db.Devices.Add(new Device { Id = 21, Name = "Probe", Type = "SmartMeter", Location = "H", ProjectId = 1, Status = "online", OwnerId = 1 });
        db.DeviceTelemetry.Add(new DeviceTelemetry { DeviceId = 21, EventId = "interrupted", OccurredAt = DateTimeOffset.UtcNow, Status = "online", ReportedJson = "{}", EnergyKwh = 1 });
        await db.SaveChangesAsync();
        var sink = new RecordingSink { Available = false };
        var service = new DeviceTelemetryService(db, sink);

        Assert.True(await service.IngestAsync(new TelemetryMessage(21, "interrupted", DateTimeOffset.UtcNow, "online", "{}", 1)));

        Assert.Single(await db.TelemetryRecoveries.Where(x => x.EventId == "interrupted").ToListAsync());
    }

    private static IoBuildDbContext TestDb() => new(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class RecordingPublisher : IDeviceMqttPublisher
    {
        public List<string> Topics { get; } = [];
        public Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default) { Topics.Add(topic); return Task.CompletedTask; }
    }

    private sealed class RecordingSink : IInfluxTelemetrySink
    {
        public bool Available { get; set; } = true;
        public Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default)
        {
            if (!Available) throw new HttpRequestException("offline");
            return Task.CompletedTask;
        }
    }
}

[Trait("Context", "Devices")]
[Trait("Capability", "CommandTelemetry")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class IoTParityCorrectionTests
{
    [Fact]
    [Trait("Category", "IoT")]
    public async Task Owner_command_requires_owner_role_device_unit_and_matching_unit_ownership()
    {
        await using var db = Db();
        db.Devices.Add(new Device { Id = 40, Name = "Unit light", Type = "SmartLight", Location = "U", ProjectId = 1, UnitId = 71, OwnerId = 2, Status = "online" });
        db.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = 71, OwnerUserId = 2 });
        await db.SaveChangesAsync();
        var commands = new DeviceCommandService(db, new Publisher());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => commands.SendAuthorizedAsync(40, 2, "Builder", "brightness", JsonSerializer.SerializeToElement(50)));
        await commands.SendAuthorizedAsync(40, 2, "Owner", "brightness", JsonSerializer.SerializeToElement(50));
        Assert.Single(await db.DeviceCommands.ToListAsync());
    }

    [Fact]
    [Trait("Category", "IoT")]
    public async Task Owner_command_self_heals_device_owner_id_when_unit_is_owned_by_requesting_user()
    {
        await using var db = Db();
        // Device was created by builder so OwnerId is 99 (not owner 2)
        db.Devices.Add(new Device { Id = 45, Name = "Unit light", Type = "SmartLight", Location = "U", ProjectId = 1, UnitId = 71, OwnerId = 99, Status = "online" });
        db.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = 71, OwnerUserId = 2 });
        await db.SaveChangesAsync();
        var commands = new DeviceCommandService(db, new Publisher());

        // Even though device.OwnerId was 99, since Owner 2 owns unit 71, it self-heals and authorizes!
        await commands.SendAuthorizedAsync(45, 2, "Owner", "brightness", JsonSerializer.SerializeToElement(75));
        var dev = await db.Devices.FindAsync(45);
        Assert.Equal(2, dev!.OwnerId);
        Assert.Single(await db.DeviceCommands.ToListAsync());
    }

    private static IoBuildDbContext Db() => new(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class Publisher : IDeviceMqttPublisher { public Task PublishAsync(string topic, string payload, bool qos1, bool retain, CancellationToken cancellationToken = default) => Task.CompletedTask; }
}
