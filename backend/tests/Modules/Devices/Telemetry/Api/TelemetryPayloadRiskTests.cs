using System.Net;
using System.Text;

namespace IoBuild.Modules.Tests.Devices.Control.Api;

public sealed partial class DeviceTiersTests
{
    [Fact]
    [Trait("Flow", "DEVICES.CONTROL")]
    [Trait("Layer", "Api")]
    [Trait("Risk", "D")]
    public async Task TELEMETRY_FUZZ_unknown_or_malformed_never_server_errors()
    {
        await using var factory = new DeviceApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, SeedUnitDeviceAsync);
        var payloads = new[]
        {
            "{\"deviceId\":611,\"eventId\":\"e1\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{}\",\"energyKwh\":0.1}",
            "{\"deviceId\":999999,\"eventId\":\"e2\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{}\",\"energyKwh\":0.1}",
            "{\"deviceId\":-3,\"eventId\":\"e3\",\"occurredAt\":\"2026-09-17T10:00:00Z\",\"status\":\"online\",\"reportedJson\":\"{}\",\"energyKwh\":0.1}",
            "{\"deviceId\":611}",
        };
        foreach (var payload in payloads)
        {
            using var response = await client.PostAsync("/api/v1/devices/telemetry",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            Assert.True(
                response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
                $"Telemetry fuzz returned {response.StatusCode}");
        }
        using (var malformed = new StringContent("{not-json", Encoding.UTF8, "application/json"))
        {
            using var response = await client.PostAsync("/api/v1/devices/telemetry", malformed);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
