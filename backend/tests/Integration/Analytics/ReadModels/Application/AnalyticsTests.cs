using IoBuild.Api.Analytics;
using IoBuild.Api.Analytics.Application.Internal.CommandServices;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Model.Queries;
using IoBuild.Api.Analytics.Infrastructure.InfluxDB;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Integration.Tests.Analytics.ReadModels.Application;

[Trait("Context", "Analytics")]
[Trait("Capability", "ReadModels")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class AnalyticsTests
{
    private static IoBuildDbContext Db() => new(new DbContextOptionsBuilder<IoBuildDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // ── Builder dashboard ──

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task BuilderDashboard_returns_zeroed_metrics_when_empty()
    {
        await using var db = Db();
        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), new FakeLiveDeviceStatusService());

        var metrics = await service.Handle(new GetBuilderDashboardQuery(99));

        Assert.NotNull(metrics);
        Assert.Equal(0, metrics!.TotalDevices);
        Assert.Equal(0, metrics.OnlineDevices);
        Assert.Equal(0, metrics.OfflineDevices);
        Assert.Equal(0, metrics.ActiveProjectsCount);
        Assert.Equal(0, metrics.TotalUnits);
        Assert.Equal(0, metrics.OccupiedUnits);
        Assert.Equal(0, metrics.OccupancyRate);
        Assert.Empty(metrics.DevicesByType);
        Assert.Empty(metrics.ProjectsOverview);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task BuilderDashboard_returns_correct_counts_when_seeded()
    {
        await using var db = Db();
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "Park", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 2, BuilderUserId = 10, Name = "Lake", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.UnitProjections.Add(new UnitProjection { UnitId = 100, ProjectId = 1, BuilderUserId = 10, Status = "Occupied", LastEventAt = DateTime.UtcNow });
        db.UnitProjections.Add(new UnitProjection { UnitId = 101, ProjectId = 1, BuilderUserId = 10, Status = "Available", LastEventAt = DateTime.UtcNow });
        db.UnitProjections.Add(new UnitProjection { UnitId = 102, ProjectId = 2, BuilderUserId = 10, Status = "Occupied", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 2, ProjectId = 1, DeviceType = "SmartLight", Status = "active", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 3, ProjectId = 1, DeviceType = "SmartLight", Status = "offline", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 4, ProjectId = 2, DeviceType = "WaterSensor", Status = "online", LastEventAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), new FakeLiveDeviceStatusService());
        var metrics = await service.Handle(new GetBuilderDashboardQuery(10));

        Assert.Equal(4, metrics!.TotalDevices);
        Assert.Equal(3, metrics.OnlineDevices); // online + active
        Assert.Equal(1, metrics.OfflineDevices);
        Assert.Equal(2, metrics.ActiveProjectsCount);
        Assert.Equal(3, metrics.TotalUnits);
        Assert.Equal(2, metrics.OccupiedUnits);
        Assert.Equal(66.67, metrics.OccupancyRate, 1);
        Assert.Equal(2, metrics.DevicesByType["SmartLight"]);
        Assert.Equal(1, metrics.DevicesByType["SmartMeter"]);
        Assert.Equal(2, metrics.ProjectsOverview.Count);
        var park = metrics.ProjectsOverview.First(p => (int)p["id"] == 1);
        Assert.Equal(2, park["totalUnits"]);
        Assert.Equal(1, park["occupiedUnits"]);
        Assert.Equal(3, park["deviceCount"]);
        Assert.Empty(metrics.TemperatureHistory);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task BuilderDashboard_temperature_history_empty_when_no_temperature_devices_and_populates_when_present()
    {
        await using var db = Db();
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "P", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow });
        db.DeviceTelemetry.Add(new IoBuild.Api.Devices.Domain.Model.Aggregates.DeviceTelemetry
        {
            DeviceId = 1,
            EventId = "evt-meter-temp",
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
            Status = "online",
            ReportedJson = "{}",
            EnergyKwh = 1.0,
            TemperatureC = 25.0
        });
        await db.SaveChangesAsync();

        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), new FakeLiveDeviceStatusService());
        var metricsWithoutTempSensor = await service.Handle(new GetBuilderDashboardQuery(10));

        // SmartMeter is not a temperature device -> TemperatureHistory must be empty
        Assert.Empty(metricsWithoutTempSensor!.TemperatureHistory);

        // Add a temperature sensor
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 2, ProjectId = 1, DeviceType = "TemperatureSensor", Status = "online", LastEventAt = DateTime.UtcNow });
        db.DeviceTelemetry.Add(new IoBuild.Api.Devices.Domain.Model.Aggregates.DeviceTelemetry
        {
            DeviceId = 2,
            EventId = "evt-temp-sensor",
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-1),
            Status = "online",
            ReportedJson = "{}",
            EnergyKwh = 0.1,
            TemperatureC = 23.5
        });
        await db.SaveChangesAsync();

        var metricsWithTempSensor = await service.Handle(new GetBuilderDashboardQuery(10));
        Assert.NotEmpty(metricsWithTempSensor!.TemperatureHistory);
        Assert.Equal(23.5, metricsWithTempSensor.TemperatureHistory[0].Value);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task BuilderDashboard_online_taxonomy_via_effective_status()
    {
        await using var db = Db();
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "P", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "offline", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 2, ProjectId = 1, DeviceType = "SmartMeter", Status = "offline", LastEventAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var liveStatus = new FakeLiveDeviceStatusService(new Dictionary<string, string> { ["1"] = "online" });
        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), liveStatus);
        var metrics = await service.Handle(new GetBuilderDashboardQuery(10));

        Assert.Equal(1, metrics!.OnlineDevices);
        Assert.Equal(1, metrics.OfflineDevices);
    }

    // ── Owner dashboard ──

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task OwnerDashboard_returns_zeroed_metrics_when_empty()
    {
        await using var db = Db();
        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), new FakeLiveDeviceStatusService());
        var metrics = await service.Handle(new GetOwnerDashboardQuery(50));
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics!.TotalDevices);
        Assert.Equal(0, metrics.MyUnitsCount);
        Assert.Empty(metrics.DeviceHealthStatus);
        Assert.Empty(metrics.MyUnitsDetails);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task OwnerDashboard_returns_correct_counts_when_seeded()
    {
        await using var db = Db();
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "Park", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.UnitProjections.Add(new UnitProjection { UnitId = 100, ProjectId = 1, BuilderUserId = 10, OwnerUserId = 50, Status = "Occupied", Floor = 2, RoomNumber = "201", LastEventAt = DateTime.UtcNow });
        db.UnitProjections.Add(new UnitProjection { UnitId = 101, ProjectId = 1, BuilderUserId = 10, OwnerUserId = 50, Status = "Available", Floor = 2, RoomNumber = "202", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 1, UnitId = 100, DeviceType = "SmartLight", Status = "online", DeviceName = "Light 1", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 2, UnitId = 100, DeviceType = "AirConditioner", Status = "offline", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 3, UnitId = 101, DeviceType = "SmartLight", Status = "active", LastEventAt = DateTime.UtcNow });
        // Floor device — should NOT count for owner
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 4, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), new FakeLiveDeviceStatusService());
        var metrics = await service.Handle(new GetOwnerDashboardQuery(50));

        Assert.Equal(3, metrics!.TotalDevices);
        Assert.Equal(2, metrics.OnlineDevices); // online + active
        Assert.Equal(1, metrics.OfflineDevices);
        Assert.Equal(2, metrics.MyUnitsCount);
        Assert.Equal(3, metrics.DeviceHealthStatus.Count);
        Assert.Equal(2, metrics.MyUnitsDetails.Count);
        Assert.Contains(metrics.DeviceHealthStatus, d => d.DeviceName == "Light 1");
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task OwnerDashboard_self_heals_missing_projections_from_real_units_and_devices()
    {
        await using var db = Db();
        db.Projects.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Project { Id = 5, BuilderId = 10, Name = "Highland Tower" });
        db.Units.Add(new IoBuild.Api.Publishing.Domain.Model.Aggregates.Unit(5, "501", null, 5, "501") { Id = 80, OwnerId = 60, OwnerEmail = "owner60@test.com", Status = "occupied" });
        db.Devices.Add(new IoBuild.Api.Devices.Domain.Model.Aggregates.Device { Id = 77, Name = "Bedroom Light", Type = "SmartLight", ProjectId = 5, UnitId = 80, OwnerId = 60, Status = "online" });
        await db.SaveChangesAsync();

        // Note: No UnitProjections, DeviceProjections, or ProjectProjections were seeded!
        var service = new AnalyticsQueryService(db, new FakeLiveEnergyService(), new FakeLiveDeviceStatusService());
        var metrics = await service.Handle(new GetOwnerDashboardQuery(60));

        Assert.NotNull(metrics);
        Assert.Equal(1, metrics!.MyUnitsCount);
        Assert.Equal(1, metrics.TotalDevices);
        Assert.Equal(1, metrics.OnlineDevices);
        Assert.Single(metrics.MyUnitsDetails);
        Assert.Equal("Highland Tower", metrics.MyUnitsDetails[0]["projectName"]);
    }

    // ── Live energy aggregation ──

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task LiveEnergy_returns_empty_when_no_devices()
    {
        await using var db = Db();
        var energy = new FakeLiveEnergyService(new[] { new EnergyMinutePoint(DateTime.UtcNow, 1.5) });
        var service = new AnalyticsQueryService(db, energy, new FakeLiveDeviceStatusService());
        var result = await service.Handle(new GetBuilderLiveEnergyQuery(10, 10));
        Assert.Empty(result);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task LiveEnergy_returns_aggregated_for_builder_devices()
    {
        await using var db = Db();
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "P", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var points = new[] { new EnergyMinutePoint(DateTime.UtcNow, 2.5), new EnergyMinutePoint(DateTime.UtcNow.AddMinutes(-1), 1.0) };
        var energy = new FakeLiveEnergyService(points);
        var service = new AnalyticsQueryService(db, energy, new FakeLiveDeviceStatusService());
        var result = (await service.Handle(new GetBuilderLiveEnergyQuery(10, 10))).ToList();
        Assert.Equal(2, result.Count);
        Assert.Single(energy.CapturedDeviceIds);
        Assert.Contains("1", energy.CapturedDeviceIds);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task LiveEnergy_returns_aggregated_for_owner_devices()
    {
        await using var db = Db();
        db.UnitProjections.Add(new UnitProjection { UnitId = 100, ProjectId = 1, BuilderUserId = 10, OwnerUserId = 50, Status = "Occupied", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 5, UnitId = 100, DeviceType = "SmartLight", Status = "online", LastEventAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var points = new[] { new EnergyMinutePoint(DateTime.UtcNow, 3.0) };
        var energy = new FakeLiveEnergyService(points);
        var service = new AnalyticsQueryService(db, energy, new FakeLiveDeviceStatusService());
        var result = (await service.Handle(new GetOwnerLiveEnergyQuery(50, 5))).ToList();
        Assert.Single(result);
        Assert.Contains("5", energy.CapturedDeviceIds);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task LiveEnergy_fallback_returns_empty_when_influx_unavailable()
    {
        await using var db = Db();
        db.ProjectProjections.Add(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "P", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        db.DeviceProjections.Add(new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var energy = new FakeLiveEnergyService(shouldThrow: true);
        var service = new AnalyticsQueryService(db, energy, new FakeLiveDeviceStatusService());
        var result = await service.Handle(new GetBuilderLiveEnergyQuery(10, 10));
        Assert.Empty(result);
    }

    // ── LWW and importer idempotency ──

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Projection_LWW_stale_event_is_ignored()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        var now = DateTime.UtcNow;
        await importer.UpsertDeviceAsync(new DeviceProjection { DeviceId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = now });
        await importer.UpsertDeviceAsync(new DeviceProjection { DeviceId = 1, DeviceType = "SmartMeter", Status = "offline", LastEventAt = now.AddMinutes(-5) });
        var row = await db.DeviceProjections.FindAsync(1);
        Assert.Equal("online", row!.Status);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Projection_LWW_newer_event_overwrites()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        var now = DateTime.UtcNow;
        await importer.UpsertDeviceAsync(new DeviceProjection { DeviceId = 1, DeviceType = "SmartMeter", Status = "offline", LastEventAt = now });
        await importer.UpsertDeviceAsync(new DeviceProjection { DeviceId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = now.AddMinutes(5) });
        var row = await db.DeviceProjections.FindAsync(1);
        Assert.Equal("online", row!.Status);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Importer_collision_handling_upsert_does_not_duplicate()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        await importer.UpsertProjectAsync(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "Park", Status = "OnGoing", LastEventAt = DateTime.UtcNow });
        await importer.UpsertProjectAsync(new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "Park Updated", Status = "Finished", LastEventAt = DateTime.UtcNow.AddMinutes(1) });
        Assert.Equal(1, await db.ProjectProjections.CountAsync());
        var row = await db.ProjectProjections.FindAsync(1);
        Assert.Equal("Park Updated", row!.Name);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Importer_invalid_ref_nulling_device_with_missing_project_nulls_projectId()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        await importer.UpsertDeviceAsync(new DeviceProjection { DeviceId = 1, ProjectId = 999, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow });
        var row = await db.DeviceProjections.FindAsync(1);
        Assert.Null(row!.ProjectId);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Importer_repeatability_same_input_yields_same_counts()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        var projects = new[] { new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "P1", Status = "OnGoing", LastEventAt = DateTime.UtcNow } };
        var units = new[] { new UnitProjection { UnitId = 100, ProjectId = 1, BuilderUserId = 10, Status = "Occupied", LastEventAt = DateTime.UtcNow } };
        var devices = new[] { new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow } };

        await importer.ImportAsync(projects, units, devices);
        var firstCounts = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
        await importer.ImportAsync(projects, units, devices);
        var secondCounts = (await db.ProjectProjections.CountAsync(), await db.UnitProjections.CountAsync(), await db.DeviceProjections.CountAsync());
        Assert.Equal(firstCounts, secondCounts);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Importer_counts_are_correct_after_import()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        var projects = new[] { new ProjectProjection { ProjectId = 1, BuilderUserId = 10, Name = "P1", Status = "OnGoing", LastEventAt = DateTime.UtcNow }, new ProjectProjection { ProjectId = 2, BuilderUserId = 10, Name = "P2", Status = "OnGoing", LastEventAt = DateTime.UtcNow } };
        var units = new[] { new UnitProjection { UnitId = 100, ProjectId = 1, BuilderUserId = 10, Status = "Occupied", LastEventAt = DateTime.UtcNow } };
        var devices = new[] { new DeviceProjection { DeviceId = 1, ProjectId = 1, DeviceType = "SmartMeter", Status = "online", LastEventAt = DateTime.UtcNow }, new DeviceProjection { DeviceId = 2, ProjectId = 2, DeviceType = "WaterSensor", Status = "online", LastEventAt = DateTime.UtcNow } };
        await importer.ImportAsync(projects, units, devices);
        Assert.Equal(2, await db.ProjectProjections.CountAsync());
        Assert.Equal(1, await db.UnitProjections.CountAsync());
        Assert.Equal(2, await db.DeviceProjections.CountAsync());
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Importer_placeholder_for_unit_owner_matched_event_creates_row_when_missing()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        await importer.ApplyUnitOwnerMatchedAsync(unitId: 500, projectId: 1, ownerUserId: 99, ownerEmail: "owner@example.com", occurredOn: DateTime.UtcNow);
        var row = await db.UnitProjections.FindAsync(500);
        Assert.NotNull(row);
        Assert.Equal(99, row!.OwnerUserId);
        Assert.Equal("Occupied", row.Status);
    }

    [Fact]
    [Trait("Category", "Analytics")]
    public async Task Importer_checkpoint_is_idempotent_across_restarts()
    {
        await using var db = Db();
        var importer = new AnalyticsProjectionImporter(db);
        var now = DateTime.UtcNow;
        await importer.UpsertDeviceAsync(new DeviceProjection { DeviceId = 10, DeviceType = "SmartMeter", Status = "online", LastEventAt = now });
        // Simulate restart: new importer instance with same db
        var importer2 = new AnalyticsProjectionImporter(db);
        await importer2.UpsertDeviceAsync(new DeviceProjection { DeviceId = 10, DeviceType = "SmartMeter", Status = "online", LastEventAt = now });
        Assert.Equal(1, await db.DeviceProjections.CountAsync());
    }

    // ── Fake Influx adapters ──

    private sealed class FakeLiveEnergyService : ILiveEnergyService
    {
        private readonly IEnumerable<EnergyMinutePoint>? _points;
        private readonly bool _shouldThrow;
        public List<string> CapturedDeviceIds { get; } = [];
        public FakeLiveEnergyService(IEnumerable<EnergyMinutePoint>? points = null, bool shouldThrow = false) { _points = points; _shouldThrow = shouldThrow; }
        public Task<IEnumerable<EnergyMinutePoint>> GetAggregatedAsync(IEnumerable<string> deviceIds, int minutes, CancellationToken ct = default)
        {
            CapturedDeviceIds.AddRange(deviceIds);
            if (_shouldThrow) throw new HttpRequestException("influx unavailable");
            if (_points is not null && CapturedDeviceIds.Count > 0) return Task.FromResult(_points);
            return Task.FromResult<IEnumerable<EnergyMinutePoint>>([]);
        }
    }

    private sealed class FakeLiveDeviceStatusService : ILiveDeviceStatusService
    {
        private readonly Dictionary<string, string> _statuses;
        public FakeLiveDeviceStatusService(Dictionary<string, string>? statuses = null) => _statuses = statuses ?? [];
        public Task<Dictionary<string, string>> GetLatestStatusesAsync(IEnumerable<string> deviceIds, CancellationToken ct = default)
        {
            var result = deviceIds.Where(id => _statuses.ContainsKey(id)).ToDictionary(id => id, id => _statuses[id]);
            return Task.FromResult(result);
        }
    }
}
