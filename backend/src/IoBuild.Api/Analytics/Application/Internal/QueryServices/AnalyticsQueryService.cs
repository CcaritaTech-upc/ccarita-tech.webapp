using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Analytics.Domain.Model.Queries;
using IoBuild.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IoBuild.Api.Analytics.Application.Internal.QueryServices;

public interface ILiveEnergyService
{
    Task<IEnumerable<EnergyMinutePoint>> GetAggregatedAsync(IEnumerable<string> deviceIds, int minutes, CancellationToken ct = default);
}

public interface ILiveDeviceStatusService
{
    Task<Dictionary<string, string>> GetLatestStatusesAsync(IEnumerable<string> deviceIds, CancellationToken ct = default);
}

public interface IAnalyticsQueryService
{
    Task<BuilderMetrics?> Handle(GetBuilderDashboardQuery query, CancellationToken ct = default);
    Task<OwnerMetrics?> Handle(GetOwnerDashboardQuery query, CancellationToken ct = default);
    Task<IEnumerable<HistoricalDataPoint>> Handle(GetHistoricalDataQuery query, CancellationToken ct = default);
    Task<IEnumerable<EnergyMinutePoint>> Handle(GetBuilderLiveEnergyQuery query, CancellationToken ct = default);
    Task<IEnumerable<EnergyMinutePoint>> Handle(GetOwnerLiveEnergyQuery query, CancellationToken ct = default);
}

public sealed class AnalyticsQueryService : IAnalyticsQueryService
{
    private static readonly HashSet<string> OnlineStatuses = new(StringComparer.OrdinalIgnoreCase) { "online", "active", "idle", "standby" };
    private static bool IsOnline(string? status) => OnlineStatuses.Contains((status ?? string.Empty).Trim());

    private readonly IoBuildDbContext _db;
    private readonly ILiveEnergyService _liveEnergyService;
    private readonly ILiveDeviceStatusService _liveDeviceStatusService;
    private readonly ILogger<AnalyticsQueryService>? _logger;

    public AnalyticsQueryService(IoBuildDbContext db, ILiveEnergyService liveEnergyService, ILiveDeviceStatusService liveDeviceStatusService, ILogger<AnalyticsQueryService>? logger = null)
    {
        _db = db;
        _liveEnergyService = liveEnergyService;
        _liveDeviceStatusService = liveDeviceStatusService;
        _logger = logger;
    }

    private async Task<Dictionary<int, string>> ResolveEffectiveStatusesAsync(IReadOnlyCollection<DeviceProjection> devices, CancellationToken ct = default)
    {
        if (devices.Count == 0) return new Dictionary<int, string>();
        var liveStatuses = await _liveDeviceStatusService.GetLatestStatusesAsync(devices.Select(d => d.DeviceId.ToString()), ct);
        var result = new Dictionary<int, string>();
        var missingDevices = new List<DeviceProjection>();
        foreach (var d in devices)
        {
            if (liveStatuses.TryGetValue(d.DeviceId.ToString(), out var s) && !string.IsNullOrWhiteSpace(s))
            {
                result[d.DeviceId] = s;
            }
            else
            {
                missingDevices.Add(d);
            }
        }
        if (missingDevices.Count > 0)
        {
            foreach (var d in missingDevices)
            {
                var latestStatus = await _db.DeviceTelemetry
                    .Where(t => t.DeviceId == d.DeviceId)
                    .OrderByDescending(t => t.OccurredAt)
                    .Select(t => t.Status)
                    .FirstOrDefaultAsync(ct);
                result[d.DeviceId] = !string.IsNullOrWhiteSpace(latestStatus) ? latestStatus : d.Status;
            }
        }
        return result;
    }

    public async Task<BuilderMetrics?> Handle(GetBuilderDashboardQuery query, CancellationToken ct = default)
    {
        using var _ = await Domain.Model.AnalyticsSyncGates.EnterAsync(query.UserId, ct);
        _logger?.LogInformation("Building builder dashboard for user {UserId}", query.UserId);

        // 1. Sync on-demand from primary Projects table if not yet projected
        var realProjects = await _db.Projects.Where(p => p.BuilderId == query.UserId).ToListAsync(ct);
        if (realProjects.Count > 0)
        {
            var pIds = realProjects.Select(p => p.Id).ToList();
            var existingProjectProjIds = await _db.ProjectProjections
                .Where(p => pIds.Contains(p.ProjectId))
                .Select(p => p.ProjectId)
                .ToListAsync(ct);

            var missingProjects = realProjects.Where(p => !existingProjectProjIds.Contains(p.Id)).ToList();
            foreach (var p in missingProjects)
            {
                _db.ProjectProjections.Add(new ProjectProjection
                {
                    ProjectId = p.Id,
                    BuilderUserId = p.BuilderId,
                    Name = p.Name,
                    Status = "Active",
                    LastEventAt = p.CreatedAt.UtcDateTime
                });
            }

            // Sync units for these projects
            var realUnits = await _db.Units.Where(u => pIds.Contains(u.ProjectId)).ToListAsync(ct);
            var uIds = realUnits.Select(u => u.Id).ToList();
            var existingUnitProjIds = await _db.UnitProjections
                .Where(u => uIds.Contains(u.UnitId))
                .Select(u => u.UnitId)
                .ToListAsync(ct);

            var missingUnits = realUnits.Where(u => !existingUnitProjIds.Contains(u.Id)).ToList();
            foreach (var u in missingUnits)
            {
                var isOcc = !string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerId.HasValue || string.Equals(u.Status, "Occupied", StringComparison.OrdinalIgnoreCase);
                _db.UnitProjections.Add(new UnitProjection
                {
                    UnitId = u.Id,
                    ProjectId = u.ProjectId,
                    BuilderUserId = query.UserId,
                    OwnerUserId = u.OwnerId,
                    OwnerEmail = u.OwnerEmail,
                    Status = isOcc ? "Occupied" : "Available",
                    Floor = u.Floor,
                    RoomNumber = u.RoomNumber,
                    LastEventAt = DateTime.UtcNow
                });
            }

            // Sync devices for these projects
            var realDevices = await _db.Devices.Where(d => pIds.Contains(d.ProjectId)).ToListAsync(ct);
            var dIds = realDevices.Select(d => d.Id).ToList();
            var existingDeviceProjIds = await _db.DeviceProjections
                .Where(d => dIds.Contains(d.DeviceId))
                .Select(d => d.DeviceId)
                .ToListAsync(ct);

            var missingDevices = realDevices.Where(d => !existingDeviceProjIds.Contains(d.Id)).ToList();
            foreach (var d in missingDevices)
            {
                _db.DeviceProjections.Add(new DeviceProjection
                {
                    DeviceId = d.Id,
                    ProjectId = d.ProjectId,
                    UnitId = d.UnitId,
                    DeviceName = d.Name,
                    DeviceType = d.Type,
                    Status = d.Status,
                    OwnerUserId = d.OwnerId,
                    LastEventAt = DateTime.UtcNow
                });
            }

            if (missingProjects.Count > 0 || missingUnits.Count > 0 || missingDevices.Count > 0)
            {
                await _db.SaveChangesAsync(ct);
            }
        }

        var builderProjectIds = await _db.ProjectProjections.Where(p => p.BuilderUserId == query.UserId).Select(p => p.ProjectId).ToListAsync(ct);
        var activeProjectsCount = builderProjectIds.Count;

        var devices = await _db.DeviceProjections.Where(d => d.ProjectId != null && _db.ProjectProjections.Any(p => p.BuilderUserId == query.UserId && p.ProjectId == d.ProjectId!.Value)).ToListAsync(ct);
        var effectiveStatuses = await ResolveEffectiveStatusesAsync(devices, ct);
        var totalDevices = devices.Count;
        var onlineDevices = devices.Count(d => IsOnline(effectiveStatuses[d.DeviceId]));
        var offlineDevices = devices.Count(d => !IsOnline(effectiveStatuses[d.DeviceId]));
        var devicesByType = devices.GroupBy(d => d.DeviceType).ToDictionary(g => g.Key, g => g.Count());
        var units = await _db.UnitProjections.Where(u => u.BuilderUserId == query.UserId).ToListAsync(ct);
        var totalUnits = units.Count;
        var occupiedUnits = units.Count(u => u.Status.Equals("Occupied", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerUserId.HasValue);
        var occupancyRate = totalUnits > 0 ? (double)occupiedUnits / totalUnits * 100 : 0;
        var projects = await _db.ProjectProjections.Where(p => p.BuilderUserId == query.UserId).ToListAsync(ct);

        var realProjectLocations = await _db.Projects
            .Where(p => builderProjectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Location ?? "N/A", ct);

        var projectsOverview = projects.Select(p =>
        {
            var pUnits = units.Where(u => u.ProjectId == p.ProjectId).ToList();
            var pOccupied = pUnits.Count(u => u.Status.Equals("Occupied", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(u.OwnerEmail) || u.OwnerUserId.HasValue);
            var pTotal = pUnits.Count;
            var pDevices = devices.Count(d => d.ProjectId == p.ProjectId);
            return new Dictionary<string, object>
            {
                ["id"] = p.ProjectId,
                ["name"] = p.Name,
                ["location"] = realProjectLocations.GetValueOrDefault(p.ProjectId, "N/A"),
                ["status"] = p.Status,
                ["totalUnits"] = pTotal,
                ["occupiedUnits"] = pOccupied,
                ["occupancyRate"] = pTotal > 0 ? Math.Round((double)pOccupied / pTotal * 100, 1) : 0.0,
                ["deviceCount"] = pDevices
            };
        }).ToList<Dictionary<string, object>>();

        var deviceIds = devices.Select(d => d.DeviceId).ToList();
        var hourlyEnergyData = new List<HistoricalDataPoint>();
        var monthlyOccupancy = new List<HistoricalDataPoint>();
        var temperatureHistory = new List<HistoricalDataPoint>();

        if (activeProjectsCount > 0)
        {
            var now = DateTime.UtcNow;

            // Monthly occupancy: last 6 months (based on real occupancy rate and project timeline)
            var projectStartDates = realProjects.Select(p => p.CreatedAt.UtcDateTime).ToList();
            var earliestProject = projectStartDates.Count > 0 ? projectStartDates.Min() : now;
            var earliestMonthStart = new DateTime(earliestProject.Year, earliestProject.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int m = 5; m >= 0; m--)
            {
                var monthDate = now.AddMonths(-m);
                var startOfMonth = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                if (startOfMonth < earliestMonthStart) continue;

                monthlyOccupancy.Add(new HistoricalDataPoint { Timestamp = startOfMonth, Value = Math.Round(occupancyRate, 1), Metric = "occupancy" });
            }

            // Real Hourly energy: last 24 hours from actual device telemetry
            var last24h = now.AddHours(-24);
            var telemetry24h = deviceIds.Count > 0
                ? await _db.DeviceTelemetry
                    .Where(t => deviceIds.Contains(t.DeviceId) && t.OccurredAt >= last24h)
                    .Select(t => new { t.OccurredAt, t.EnergyKwh, t.TemperatureC })
                    .ToListAsync(ct)
                : [];

            if (telemetry24h.Count > 0)
            {
                var hourGroups = telemetry24h
                    .GroupBy(t => new DateTime(t.OccurredAt.Year, t.OccurredAt.Month, t.OccurredAt.Day, t.OccurredAt.Hour, 0, 0, DateTimeKind.Utc))
                    .OrderBy(g => g.Key);

                foreach (var g in hourGroups)
                {
                    hourlyEnergyData.Add(new HistoricalDataPoint
                    {
                        Timestamp = g.Key,
                        Value = Math.Round(g.Sum(t => t.EnergyKwh), 2),
                        Metric = "energy"
                    });
                }
            }

            // Real Temperature trend: last 7 days from actual device telemetry
            var last7d = now.AddDays(-7);
            var telemetry7d = deviceIds.Count > 0
                ? await _db.DeviceTelemetry
                    .Where(t => deviceIds.Contains(t.DeviceId) && t.OccurredAt >= last7d && t.TemperatureC > 0)
                    .Select(t => new { t.OccurredAt, t.TemperatureC })
                    .ToListAsync(ct)
                : [];

            if (telemetry7d.Count > 0)
            {
                var earliestTempDate = telemetry7d.Min(t => t.OccurredAt.Date);
                var startDate = earliestTempDate > now.AddDays(-6).Date ? earliestTempDate : now.AddDays(-6).Date;
                for (var d = startDate; d <= now.Date; d = d.AddDays(1))
                {
                    var dayReadings = telemetry7d.Where(t => t.OccurredAt.Date == d).ToList();
                    if (dayReadings.Count > 0)
                    {
                        temperatureHistory.Add(new HistoricalDataPoint
                        {
                            Timestamp = d,
                            Value = Math.Round(dayReadings.Average(t => t.TemperatureC), 1),
                            Metric = "temperature"
                        });
                    }
                }
            }
        }

        var activeHours = hourlyEnergyData.Where(h => h.Value > 0).ToList();
        var avgEnergy = activeHours.Count > 0
            ? Math.Round(activeHours.Average(h => h.Value), 2)
            : 0.0;

        return new BuilderMetrics
        {
            TotalDevices = totalDevices,
            OnlineDevices = onlineDevices,
            OfflineDevices = offlineDevices,
            AlertsCount = 0,
            ActiveProjectsCount = activeProjectsCount,
            TotalUnits = totalUnits,
            OccupiedUnits = occupiedUnits,
            OccupancyRate = occupancyRate,
            EnergyEfficiencyAvg = avgEnergy,
            DevicesByType = devicesByType,
            ProjectsOverview = projectsOverview,
            TemperatureHistory = temperatureHistory,
            EnergyHistory = hourlyEnergyData,
            HourlyEnergyData = hourlyEnergyData,
            MonthlyOccupancy = monthlyOccupancy
        };
    }

    public async Task<OwnerMetrics?> Handle(GetOwnerDashboardQuery query, CancellationToken ct = default)
    {
        using var _ = await Domain.Model.AnalyticsSyncGates.EnterAsync(query.UserId, ct);
        _logger?.LogInformation("Building owner dashboard for user {UserId}", query.UserId);

        // Self-heal: ensure real units, devices, and project projections for this owner are synchronized
        var user = await _db.IamUsers.FindAsync([query.UserId], ct);
        var userEmail = user?.Email?.ToLowerInvariant();

        var realUnits = await _db.Units
            .Where(u => u.OwnerId == query.UserId || (!string.IsNullOrEmpty(userEmail) && u.OwnerEmail != null && u.OwnerEmail.ToLower() == userEmail))
            .ToListAsync(ct);

        if (realUnits.Count > 0)
        {
            var pIds = realUnits.Select(u => u.ProjectId).Distinct().ToList();
            var existingProjectProjIds = await _db.ProjectProjections
                .Where(p => pIds.Contains(p.ProjectId))
                .Select(p => p.ProjectId)
                .ToListAsync(ct);

            var missingProjects = await _db.Projects
                .Where(p => pIds.Contains(p.Id) && !existingProjectProjIds.Contains(p.Id))
                .ToListAsync(ct);

            foreach (var p in missingProjects)
            {
                _db.ProjectProjections.Add(new ProjectProjection
                {
                    ProjectId = p.Id,
                    BuilderUserId = p.BuilderId,
                    Name = p.Name,
                    Status = "OnGoing",
                    LastEventAt = DateTime.UtcNow
                });
            }

            var uIds = realUnits.Select(u => u.Id).ToList();
            var existingUnitProjs = await _db.UnitProjections
                .Where(u => uIds.Contains(u.UnitId))
                .ToListAsync(ct);
            var existingUnitProjMap = existingUnitProjs.ToDictionary(u => u.UnitId);

            foreach (var u in realUnits)
            {
                if (existingUnitProjMap.TryGetValue(u.Id, out var existingProj))
                {
                    if (existingProj.OwnerUserId != query.UserId)
                    {
                        existingProj.OwnerUserId = query.UserId;
                        existingProj.OwnerEmail = userEmail ?? existingProj.OwnerEmail;
                        existingProj.Status = "Occupied";
                        existingProj.LastEventAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    _db.UnitProjections.Add(new UnitProjection
                    {
                        UnitId = u.Id,
                        ProjectId = u.ProjectId,
                        BuilderUserId = 0,
                        OwnerUserId = query.UserId,
                        OwnerEmail = userEmail ?? u.OwnerEmail,
                        Status = "Occupied",
                        Floor = u.Floor,
                        RoomNumber = u.RoomNumber,
                        LastEventAt = DateTime.UtcNow
                    });
                }
            }

            // Sync devices for these units
            var realDevices = await _db.Devices.Where(d => d.UnitId.HasValue && uIds.Contains(d.UnitId.Value)).ToListAsync(ct);
            var dIds = realDevices.Select(d => d.Id).ToList();
            var existingDeviceProjs = await _db.DeviceProjections
                .Where(d => dIds.Contains(d.DeviceId))
                .ToListAsync(ct);
            var existingDevProjMap = existingDeviceProjs.ToDictionary(d => d.DeviceId);

            foreach (var d in realDevices)
            {
                if (d.OwnerId != query.UserId)
                {
                    d.OwnerId = query.UserId;
                }

                if (existingDevProjMap.TryGetValue(d.Id, out var dp))
                {
                    if (dp.OwnerUserId != query.UserId)
                    {
                        dp.OwnerUserId = query.UserId;
                        dp.LastEventAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    _db.DeviceProjections.Add(new DeviceProjection
                    {
                        DeviceId = d.Id,
                        ProjectId = d.ProjectId,
                        UnitId = d.UnitId,
                        DeviceName = d.Name,
                        DeviceType = d.Type,
                        Status = d.Status,
                        OwnerUserId = query.UserId,
                        LastEventAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        var devices = await _db.DeviceProjections.Where(d => d.UnitId != null && _db.UnitProjections.Any(u => u.OwnerUserId == query.UserId && u.UnitId == d.UnitId!.Value)).ToListAsync(ct);
        var effectiveStatuses = await ResolveEffectiveStatusesAsync(devices, ct);
        var totalDevices = devices.Count;
        var onlineDevices = devices.Count(d => IsOnline(effectiveStatuses[d.DeviceId]));
        var offlineDevices = devices.Count(d => !IsOnline(effectiveStatuses[d.DeviceId]));
        var deviceHealthStatus = devices.Select(d => new DeviceHealthStatus
        {
            DeviceId = d.DeviceId,
            DeviceName = d.DeviceName ?? $"{d.DeviceType} #{d.DeviceId}",
            Type = d.DeviceType,
            Status = effectiveStatuses[d.DeviceId],
            LastOnline = d.LastEventAt
        }).ToList();
        var units = await _db.UnitProjections.Where(u => u.OwnerUserId == query.UserId).ToListAsync(ct);
        var myUnitsCount = units.Count;
        var projectNames = await _db.ProjectProjections.Where(p => _db.UnitProjections.Any(u => u.OwnerUserId == query.UserId && u.ProjectId == p.ProjectId)).ToDictionaryAsync(p => p.ProjectId, p => p.Name, ct);
        var myUnitsDetails = units.Select(u => new Dictionary<string, object>
        {
            ["unitId"] = u.UnitId,
            ["projectId"] = u.ProjectId,
            ["projectName"] = projectNames.GetValueOrDefault(u.ProjectId, "Unknown"),
            ["status"] = u.Status,
            ["floor"] = u.Floor ?? 0,
            ["roomNumber"] = u.RoomNumber ?? string.Empty
        }).ToList<Dictionary<string, object>>();

        var deviceIds = devices.Select(d => d.DeviceId).ToList();
        var now = DateTime.UtcNow;
        var startOf30Days = now.AddDays(-30);

        var telemetry = deviceIds.Count > 0
            ? await _db.DeviceTelemetry
                .Where(t => deviceIds.Contains(t.DeviceId) && t.OccurredAt >= startOf30Days)
                .Select(t => new { t.DeviceId, t.OccurredAt, t.EnergyKwh, t.TemperatureC })
                .ToListAsync(ct)
            : [];

        var validTempReadings = telemetry.Where(t => t.TemperatureC > 0).ToList();
        var avgTemp = validTempReadings.Count > 0 ? Math.Round(validTempReadings.Average(t => t.TemperatureC), 1) : 0.0;

        var startOfCurrentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthReadings = telemetry.Where(t => t.OccurredAt >= startOfCurrentMonth).ToList();
        var energyThisMonth = currentMonthReadings.Count > 0
            ? Math.Round(currentMonthReadings.Sum(t => t.EnergyKwh), 2)
            : (telemetry.Count > 0 ? Math.Round(telemetry.Sum(t => t.EnergyKwh), 2) : 0.0);

        // Water devices
        var waterDeviceIds = devices
            .Where(d => d.DeviceType.Contains("Water", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.DeviceId)
            .ToHashSet();

        var waterReadingsThisMonth = currentMonthReadings.Where(t => waterDeviceIds.Contains(t.DeviceId)).ToList();
        var waterUsageThisMonth = waterReadingsThisMonth.Count > 0
            ? Math.Round(waterReadingsThisMonth.Sum(t => t.EnergyKwh), 2)
            : (waterDeviceIds.Count > 0 ? Math.Round(telemetry.Where(t => waterDeviceIds.Contains(t.DeviceId)).Sum(t => t.EnergyKwh), 2) : 0.0);

        // Daily energy: last 30 days (only from dates with real readings)
        var dailyEnergyConsumption = new List<HistoricalDataPoint>();
        if (telemetry.Count > 0)
        {
            var earliestEnergyDate = telemetry.Min(t => t.OccurredAt.Date);
            var startDate = earliestEnergyDate > now.AddDays(-29).Date ? earliestEnergyDate : now.AddDays(-29).Date;
            for (var d = startDate; d <= now.Date; d = d.AddDays(1))
            {
                var dayReadings = telemetry.Where(t => t.OccurredAt.Date == d).ToList();
                if (dayReadings.Count > 0)
                {
                    dailyEnergyConsumption.Add(new HistoricalDataPoint
                    {
                        Timestamp = d,
                        Value = Math.Round(dayReadings.Sum(t => t.EnergyKwh), 2),
                        Metric = "energy"
                    });
                }
            }
        }

        // Temperature comfort: last 7 days (only from dates with real readings)
        var temperatureHistory = new List<HistoricalDataPoint>();
        if (validTempReadings.Count > 0)
        {
            var temp7d = validTempReadings.Where(t => t.OccurredAt >= now.AddDays(-7)).ToList();
            if (temp7d.Count > 0)
            {
                var earliestTempDate = temp7d.Min(t => t.OccurredAt.Date);
                var startDate = earliestTempDate > now.AddDays(-6).Date ? earliestTempDate : now.AddDays(-6).Date;
                for (var d = startDate; d <= now.Date; d = d.AddDays(1))
                {
                    var dayReadings = temp7d.Where(t => t.OccurredAt.Date == d).ToList();
                    if (dayReadings.Count > 0)
                    {
                        temperatureHistory.Add(new HistoricalDataPoint
                        {
                            Timestamp = d,
                            Value = Math.Round(dayReadings.Average(t => t.TemperatureC), 1),
                            Metric = "temperature"
                        });
                    }
                }
            }
        }

        // Water usage: last 7 days (only from dates with real water readings)
        var waterUsageWeekly = new List<HistoricalDataPoint>();
        var waterReadings = telemetry.Where(t => waterDeviceIds.Contains(t.DeviceId)).ToList();
        if (waterReadings.Count > 0)
        {
            var earliestWaterDate = waterReadings.Min(t => t.OccurredAt.Date);
            var startDate = earliestWaterDate > now.AddDays(-6).Date ? earliestWaterDate : now.AddDays(-6).Date;
            for (var d = startDate; d <= now.Date; d = d.AddDays(1))
            {
                var dayReadings = waterReadings.Where(t => t.OccurredAt.Date == d).ToList();
                if (dayReadings.Count > 0)
                {
                    waterUsageWeekly.Add(new HistoricalDataPoint
                    {
                        Timestamp = d,
                        Value = Math.Round(dayReadings.Sum(t => t.EnergyKwh), 2),
                        Metric = "water"
                    });
                }
            }
        }

        return new OwnerMetrics
        {
            TotalDevices = totalDevices,
            OnlineDevices = onlineDevices,
            OfflineDevices = offlineDevices,
            AlertsCount = 0,
            MyUnitsCount = myUnitsCount,
            EnergyThisMonth = energyThisMonth,
            TemperatureAvg = avgTemp,
            WaterUsageThisMonth = waterUsageThisMonth,
            TemperatureHistory = temperatureHistory,
            EnergyHistory = dailyEnergyConsumption,
            DailyEnergyConsumption = dailyEnergyConsumption,
            WaterUsageWeekly = waterUsageWeekly,
            DeviceHealthStatus = deviceHealthStatus,
            MyUnitsDetails = myUnitsDetails
        };
    }

    public Task<IEnumerable<HistoricalDataPoint>> Handle(GetHistoricalDataQuery query, CancellationToken ct = default)
    {
        _logger?.LogInformation("GetHistoricalData called for project {ProjectId} — telemetry out of scope, returning empty", query.ProjectId);
        return Task.FromResult<IEnumerable<HistoricalDataPoint>>([]);
    }

    public async Task<IEnumerable<EnergyMinutePoint>> Handle(GetBuilderLiveEnergyQuery query, CancellationToken ct = default)
    {
        _logger?.LogInformation("GetBuilderLiveEnergy for user {UserId}, {Minutes}m", query.UserId, query.Minutes);
        var intIds = await _db.DeviceProjections
            .Where(d => d.ProjectId != null && _db.ProjectProjections.Any(p => p.BuilderUserId == query.UserId && p.ProjectId == d.ProjectId!.Value))
            .Select(d => d.DeviceId)
            .ToListAsync(ct);
        if (intIds.Count == 0) return [];

        try
        {
            var influxResults = (await _liveEnergyService.GetAggregatedAsync(intIds.Select(id => id.ToString()), query.Minutes, ct)).ToList();
            if (influxResults.Count > 0) return influxResults;
        }
        catch { }

        // Fallback to MySQL device_telemetry
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-query.Minutes);
        var telemetry = await _db.DeviceTelemetry
            .Where(t => intIds.Contains(t.DeviceId) && t.OccurredAt >= cutoff)
            .OrderBy(t => t.OccurredAt)
            .ToListAsync(ct);

        if (telemetry.Count == 0)
        {
            var latest = await _db.DeviceTelemetry
                .Where(t => intIds.Contains(t.DeviceId))
                .OrderByDescending(t => t.OccurredAt)
                .Take(intIds.Count)
                .ToListAsync(ct);
            if (latest.Count > 0)
            {
                var nowMinute = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
                return [new EnergyMinutePoint(nowMinute, Math.Round(latest.Sum(t => t.EnergyKwh), 3))];
            }
            return [];
        }

        return telemetry
            .GroupBy(t => new DateTime(t.OccurredAt.Year, t.OccurredAt.Month, t.OccurredAt.Day, t.OccurredAt.Hour, t.OccurredAt.Minute, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new EnergyMinutePoint(g.Key, Math.Round(g.Sum(t => t.EnergyKwh), 3)));
    }

    public async Task<IEnumerable<EnergyMinutePoint>> Handle(GetOwnerLiveEnergyQuery query, CancellationToken ct = default)
    {
        _logger?.LogInformation("GetOwnerLiveEnergy for user {UserId}, {Minutes}m", query.UserId, query.Minutes);
        var intIds = await _db.DeviceProjections
            .Where(d => d.UnitId != null && _db.UnitProjections.Any(u => u.OwnerUserId == query.UserId && u.UnitId == d.UnitId!.Value))
            .Select(d => d.DeviceId)
            .ToListAsync(ct);
        if (intIds.Count == 0) return [];

        try
        {
            var influxResults = (await _liveEnergyService.GetAggregatedAsync(intIds.Select(id => id.ToString()), query.Minutes, ct)).ToList();
            if (influxResults.Count > 0) return influxResults;
        }
        catch { }

        // Fallback to MySQL device_telemetry
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-query.Minutes);
        var telemetry = await _db.DeviceTelemetry
            .Where(t => intIds.Contains(t.DeviceId) && t.OccurredAt >= cutoff)
            .OrderBy(t => t.OccurredAt)
            .ToListAsync(ct);

        if (telemetry.Count == 0)
        {
            var latest = await _db.DeviceTelemetry
                .Where(t => intIds.Contains(t.DeviceId))
                .OrderByDescending(t => t.OccurredAt)
                .Take(intIds.Count)
                .ToListAsync(ct);
            if (latest.Count > 0)
            {
                var nowMinute = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0, DateTimeKind.Utc);
                return [new EnergyMinutePoint(nowMinute, Math.Round(latest.Sum(t => t.EnergyKwh), 3))];
            }
            return [];
        }

        return telemetry
            .GroupBy(t => new DateTime(t.OccurredAt.Year, t.OccurredAt.Month, t.OccurredAt.Day, t.OccurredAt.Hour, t.OccurredAt.Minute, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new EnergyMinutePoint(g.Key, Math.Round(g.Sum(t => t.EnergyKwh), 3)));
    }
}
