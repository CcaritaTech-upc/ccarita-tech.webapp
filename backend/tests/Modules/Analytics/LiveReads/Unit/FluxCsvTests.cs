using IoBuild.Api.Analytics.Infrastructure.InfluxDB;

namespace IoBuild.Modules.Tests;

public sealed partial class AnalyticsInfluxReadTests
{
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
}
