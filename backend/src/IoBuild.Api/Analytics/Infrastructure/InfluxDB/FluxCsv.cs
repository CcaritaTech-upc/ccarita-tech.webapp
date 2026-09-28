using System.Text;

namespace IoBuild.Api.Analytics.Infrastructure.InfluxDB;

/// <summary>Reads the annotated CSV that InfluxDB's /api/v2/query returns into column-name → value rows.</summary>
public static class FluxCsv
{
    public static List<Dictionary<string, string>> Parse(string csv)
    {
        var rows = new List<Dictionary<string, string>>();
        string[]? header = null;
        foreach (var line in csv.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.Length == 0) { header = null; continue; }   // blank line ends a table
            if (text[0] == '#') continue;                        // annotation rows
            var fields = SplitLine(text);
            if (header is null) { header = fields; continue; }   // first row of a table is its header
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 1; i < header.Length && i < fields.Length; i++) row[header[i]] = fields[i];
            rows.Add(row);
        }
        return rows;
    }

    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return [.. fields];
    }
}
