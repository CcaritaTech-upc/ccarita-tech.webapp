using IoBuild.Api.Devices.Application.Internal.CommandServices;

namespace IoBuild.Api.Devices.Infrastructure.InfluxDB;

public sealed class InfluxHttpTelemetrySink(HttpClient client, Microsoft.Extensions.Configuration.IConfiguration configuration) : IInfluxTelemetrySink
{
    private static DateTimeOffset _lastFailureTime = DateTimeOffset.MinValue;
    private static readonly TimeSpan CoolOffPeriod = TimeSpan.FromSeconds(60);

    public async Task WriteAsync(TelemetryMessage message, CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow - _lastFailureTime < CoolOffPeriod)
        {
            throw new HttpRequestException("InfluxDB cool-off active");
        }

        var url = configuration["Influx:Url"];
        var org = configuration["Influx:Org"];
        var bucket = configuration["Influx:Bucket"];
        var token = configuration["Influx:Token"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(org) || string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(token))
            throw new HttpRequestException("InfluxDB is not configured.");

        try
        {
            var endpoint = new Uri(new Uri(url.TrimEnd('/') + "/"), $"api/v2/write?org={Uri.EscapeDataString(org)}&bucket={Uri.EscapeDataString(bucket)}&precision=ns");
            var line = $"telemetry,deviceId={message.DeviceId} energy_kwh={message.EnergyKwh.ToString(System.Globalization.CultureInfo.InvariantCulture)},temperature_c={message.TemperatureC.ToString(System.Globalization.CultureInfo.InvariantCulture)},voltage_v={message.VoltageV.ToString(System.Globalization.CultureInfo.InvariantCulture)},status=\"{message.Status.Replace("\\", "\\\\").Replace("\"", "\\\"")}\" {message.OccurredAt.ToUnixTimeMilliseconds() * 1_000_000}";
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new StringContent(line, System.Text.Encoding.UTF8, "text/plain") };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", token);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;
            throw;
        }
    }
}
