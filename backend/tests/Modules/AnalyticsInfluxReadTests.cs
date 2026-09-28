using System.Net;
using System.Text;
using IoBuild.Api.Analytics.Infrastructure.InfluxDB;
using Microsoft.Extensions.Configuration;

namespace IoBuild.Modules.Tests;

// Convergent Testing G0/G1: ANALYTICS live reads parse what InfluxDB really returns
// (annotated CSV from /api/v2/query) instead of discarding it.
public sealed class AnalyticsInfluxReadTests
{
    // Shape produced by the LiveEnergyService Flux query: one table per _time group.
    private const string EnergyCsv =
        "#datatype,string,long,dateTime:RFC3339,double\r\n" +
        "#group,false,false,true,false\r\n" +
        "#default,_result,,,\r\n" +
        ",result,table,_time,_value\r\n" +
        ",,0,2026-09-28T21:00:00Z,1.5\r\n" +
        "\r\n" +
        "#datatype,string,long,dateTime:RFC3339,double\r\n" +
        "#group,false,false,true,false\r\n" +
        "#default,_result,,,\r\n" +
        ",result,table,_time,_value\r\n" +
        ",,1,2026-09-28T21:01:00Z,2.25\r\n";

    private const string StatusCsv =
        "#datatype,string,long,dateTime:RFC3339,dateTime:RFC3339,dateTime:RFC3339,string,string,string,string\r\n" +
        "#group,false,false,true,true,false,false,true,true,true\r\n" +
        "#default,_result,,,,,,,,\r\n" +
        ",result,table,_start,_stop,_time,_value,_field,_measurement,deviceId\r\n" +
        ",,0,2026-08-29T00:00:00Z,2026-09-28T00:00:00Z,2026-09-28T20:59:00Z,idle,status,telemetry,7\r\n" +
        ",,1,2026-08-29T00:00:00Z,2026-09-28T00:00:00Z,2026-09-28T20:59:00Z,\"on,line\",status,telemetry,8\r\n";

    [Fact]
    [Trait("Flow", "ANALYTICS.LIVE")]
    [Trait("Layer", "Unit")]
    [Trait("Risk", "A")]
    public void FLUX_CSV_ignores_annotations_and_reads_each_table_by_header_name()
    {
        var rows = FluxCsv.Parse(EnergyCsv);
        Assert.Equal(2, rows.Count);
        Assert.Equal("2026-09-28T21:00:00Z", rows[0]["_time"]);
        Assert.Equal("2.25", rows[1]["_value"]);
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.LIVE")]
    [Trait("Layer", "Unit")]
    [Trait("Risk", "B")]
    public void FLUX_CSV_handles_quoted_commas_and_empty_or_error_bodies()
    {
        var rows = FluxCsv.Parse(StatusCsv);
        Assert.Equal("on,line", rows[1]["_value"]);
        Assert.Empty(FluxCsv.Parse(""));
        Assert.Empty(FluxCsv.Parse("#datatype,string\r\n\r\n"));
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.LIVE")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "A")]
    public async Task LIVE_ENERGY_returns_ordered_points_from_influx_csv()
    {
        var service = new LiveEnergyService(Client(HttpStatusCode.OK, EnergyCsv), Configured());
        var points = (await service.GetAggregatedAsync(["1", "2"], 10)).ToList();

        Assert.Equal(2, points.Count);
        Assert.Equal(new DateTime(2026, 9, 28, 21, 0, 0, DateTimeKind.Utc), points[0].Timestamp);
        Assert.Equal(1.5, points[0].TotalEnergyKwh);
        Assert.Equal(2.25, points[1].TotalEnergyKwh);
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.LIVE")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "A")]
    public async Task LIVE_DEVICE_STATUS_maps_latest_status_by_device()
    {
        var service = new LiveDeviceStatusService(Client(HttpStatusCode.OK, StatusCsv), Configured());
        var statuses = await service.GetLatestStatusesAsync(["7", "8"]);

        Assert.Equal("idle", statuses["7"]);
        Assert.Equal("on,line", statuses["8"]);
    }

    [Fact]
    [Trait("Flow", "ANALYTICS.LIVE")]
    [Trait("Layer", "Contract")]
    [Trait("Risk", "C")]
    public async Task LIVE_READS_degrade_to_empty_when_influx_fails_is_unconfigured_or_malformed()
    {
        var down = new LiveEnergyService(Client(HttpStatusCode.InternalServerError, "boom"), Configured());
        Assert.Empty(await down.GetAggregatedAsync(["1"], 10));

        var malformed = new LiveEnergyService(Client(HttpStatusCode.OK, ",result,table,_time,_value\r\n,,0,not-a-date,NaNx\r\n"), Configured());
        Assert.Empty(await malformed.GetAggregatedAsync(["1"], 10));

        var unconfigured = new LiveEnergyService(Client(HttpStatusCode.OK, EnergyCsv), new ConfigurationBuilder().Build());
        Assert.Empty(await unconfigured.GetAggregatedAsync(["1"], 10));
        var noStatus = new LiveDeviceStatusService(Client(HttpStatusCode.OK, StatusCsv), new ConfigurationBuilder().Build());
        Assert.Empty(await noStatus.GetLatestStatusesAsync(["7"]));
    }

    private static IConfiguration Configured() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Influx:Url"] = "http://influx.test:8086",
        ["Influx:Org"] = "iobuild",
        ["Influx:Bucket"] = "iobuild-telemetry",
        ["Influx:Token"] = "token",
    }).Build();

    private static HttpClient Client(HttpStatusCode status, string body) => new(new StubHandler(status, body));

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "text/csv") });
    }
}
