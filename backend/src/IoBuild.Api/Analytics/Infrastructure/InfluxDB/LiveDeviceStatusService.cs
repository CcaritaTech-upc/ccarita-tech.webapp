using System.Text.Json;
using IoBuild.Api.Analytics.Application.Internal.QueryServices;
using Microsoft.Extensions.Logging;

namespace IoBuild.Api.Analytics.Infrastructure.InfluxDB;

public sealed class LiveDeviceStatusService : ILiveDeviceStatusService
{
    private static DateTimeOffset _lastFailureTime = DateTimeOffset.MinValue;
    private static readonly TimeSpan CoolOffPeriod = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LiveDeviceStatusService>? _logger;

    public LiveDeviceStatusService(HttpClient httpClient, IConfiguration configuration, ILogger<LiveDeviceStatusService>? logger = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> GetLatestStatusesAsync(IEnumerable<string> deviceIds, CancellationToken ct = default)
    {
        var ids = deviceIds.ToList();
        if (ids.Count == 0) return new Dictionary<string, string>();
        if (DateTimeOffset.UtcNow - _lastFailureTime < CoolOffPeriod)
        {
            return new Dictionary<string, string>();
        }
        var url = _configuration["Influx:Url"];
        var org = _configuration["Influx:Org"];
        var bucket = _configuration["Influx:Bucket"];
        var token = _configuration["Influx:Token"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(token))
        {
            return new Dictionary<string, string>();
        }
        var idFilter = string.Join(" or ", ids.Select(id => $"r.deviceId == \"{id}\""));
        var flux = $"from(bucket: \"{bucket}\") |> range(start: -30d) |> filter(fn: (r) => r._measurement == \"telemetry\" and r._field == \"status\") |> filter(fn: (r) => {idFilter}) |> group(columns: [\"deviceId\"]) |> last()";
        var endpoint = new Uri(new Uri(url.TrimEnd('/') + "/"), $"api/v2/query?org={Uri.EscapeDataString(org)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query = flux, type = "flux" }), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);
        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            var statuses = new Dictionary<string, string>();
            foreach (var row in FluxCsv.Parse(body))
                if (row.TryGetValue("deviceId", out var id) && row.TryGetValue("_value", out var status) && id.Length > 0 && status.Length > 0)
                    statuses[id] = status;
            _logger?.LogInformation("LiveDeviceStatusService: {Count} status(es) from Influx", statuses.Count);
            return statuses;
        }
        catch (Exception ex)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;
            _logger?.LogWarning(ex, "LiveDeviceStatusService: Influx query failed — cooling off for 60s, returning empty");
            return new Dictionary<string, string>();
        }
    }
}
