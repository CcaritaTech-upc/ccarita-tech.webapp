using System.Text.Json;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using IoBuild.Api.Analytics.Domain.Model.Queries;
using Microsoft.Extensions.Logging;

namespace IoBuild.Api.Analytics.Infrastructure.InfluxDB;

public sealed class LiveEnergyService : ILiveEnergyService
{
    private static DateTimeOffset _lastFailureTime = DateTimeOffset.MinValue;
    private static readonly TimeSpan CoolOffPeriod = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LiveEnergyService>? _logger;

    public LiveEnergyService(HttpClient httpClient, IConfiguration configuration, ILogger<LiveEnergyService>? logger = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<EnergyMinutePoint>> GetAggregatedAsync(IEnumerable<string> deviceIds, int minutes, CancellationToken ct = default)
    {
        var ids = deviceIds.ToList();
        if (ids.Count == 0) return [];
        if (DateTimeOffset.UtcNow - _lastFailureTime < CoolOffPeriod)
        {
            return [];
        }
        var url = _configuration["Influx:Url"];
        var org = _configuration["Influx:Org"];
        var bucket = _configuration["Influx:Bucket"];
        var token = _configuration["Influx:Token"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(token))
        {
            _logger?.LogWarning("LiveEnergyService: Influx not configured — returning empty");
            return [];
        }
        var idFilter = string.Join(" or ", ids.Select(id => $"r.deviceId == \"{id}\""));
        var flux = $"from(bucket: \"{bucket}\") |> range(start: -{minutes}m) |> filter(fn: (r) => r._measurement == \"telemetry\" and r._field == \"energy_kwh\") |> filter(fn: (r) => {idFilter}) |> aggregateWindow(every: 1m, fn: mean, createEmpty: false) |> group(columns: [\"_time\"]) |> sum()";
        var endpoint = new Uri(new Uri(url.TrimEnd('/') + "/"), $"api/v2/query?org={Uri.EscapeDataString(org)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query = flux, type = "flux" }), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            var points = FluxCsv.Parse(body)
                .Where(row => row.TryGetValue("_time", out _) && row.TryGetValue("_value", out _))
                .Select(row => new EnergyMinutePoint(
                    DateTimeOffset.Parse(row["_time"], System.Globalization.CultureInfo.InvariantCulture).UtcDateTime,
                    Math.Round(double.Parse(row["_value"], System.Globalization.CultureInfo.InvariantCulture), 3)))
                .OrderBy(point => point.Timestamp)
                .ToList();
            _logger?.LogInformation("LiveEnergyService: {Count} point(s) from Influx", points.Count);
            return points;
        }
        catch (Exception ex)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;
            _logger?.LogWarning(ex, "LiveEnergyService: Influx query failed — cooling off for 60s, returning empty");
            return [];
        }
    }
}
