using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CargoPlan.Import.Contracts;
using Xunit.Abstractions;

namespace CargoPlan.Import.Tests;

/// <summary>
/// By hand only (T10.2a): every raw text of a scan-probe.log (T10.1b, scans over the game) through the parser. The log
/// changes with every probe run, so the asserts are structural (H3) and the numbers only go to the report: each scan
/// reads a delivery for every delivery line, a reward and a view, and the scans of one route have one key over both
/// views. The log is CIG material outside the repo (CARGOPLAN_SCAN_PROBE_LOG). See CLAUDE.md, Befehle.
/// </summary>
[Trait("Category", "Slow")]
public sealed partial class ScanProbeLogTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_scan_reads_fully_and_the_scans_of_one_route_share_a_key()
    {
        var path = Environment.GetEnvironmentVariable("CARGOPLAN_SCAN_PROBE_LOG") is { Length: > 0 } value ? value
            : throw new InvalidOperationException("CARGOPLAN_SCAN_PROBE_LOG is not set; this test runs by hand, see CLAUDE.md, Befehle.");
        var scans = RawTexts(File.ReadAllLines(path)).Select((text, i) => new Scan(i + 1, text, ScannedContract.Read(text), ContractTextParser.Parse(text))).ToList();

        List<string> problems = [];
        foreach (var scan in scans)
        {
            var deliveryLines = scan.Text.Split('\n').Count(l => DeliveryLike.IsMatch(l.Trim()));
            if (scan.Parsed.Deliveries.Count == 0 || scan.Parsed.Deliveries.Count != deliveryLines)
            {
                problems.Add($"scan {scan.No}: {scan.Parsed.Deliveries.Count} deliveries read of {deliveryLines} delivery lines");
            }

            if (scan.Contract.Reward is null)
            {
                problems.Add($"scan {scan.No}: no reward");
            }

            if (scan.Contract.View == ContractView.Unknown)
            {
                problems.Add($"scan {scan.No}: view unknown");
            }
        }

        // A route: its destination–commodity pairs and reward, as the truth of a pair would name it. Pickups and max size
        // must come out the same in every scan of it, whichever view.
        var routes = scans.GroupBy(s => Route(s.Parsed)).ToList();
        foreach (var route in routes.Where(r => r.Select(s => s.Contract.Key).Distinct().Count() > 1))
        {
            problems.Add($"route {route.Key}: {route.Select(s => s.Contract.Key).Distinct().Count()} keys ({string.Join("; ", route.Select(s => s.Contract.Key).Distinct())})");
        }

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"{scans.Count} scans: {string.Join(", ", scans.GroupBy(s => s.Contract.View).Select(g => $"{g.Key} {g.Count()}"))}");
        report.AppendLine(CultureInfo.InvariantCulture, $"{routes.Count} routes, {scans.Select(s => s.Contract.Key).Distinct().Count()} keys");
        foreach (var key in scans.GroupBy(s => s.Contract.Key).OrderByDescending(g => g.Count()))
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"  {key.Count(),4} × ({string.Join(", ", key.GroupBy(s => s.Contract.View).Select(g => $"{g.Key} {g.Count()}"))})  {key.Key}");
        }

        output.WriteLine(report.ToString());
        problems.Should().BeEmpty();
    }

    /// <summary>The blocks between "raw: N" and "--- end ---" (ScanProbe's log format, T10.1b).</summary>
    private static IEnumerable<string> RawTexts(IEnumerable<string> lines)
    {
        List<string>? block = null;
        foreach (var line in lines)
        {
            if (RawStart.IsMatch(line))
            {
                block = [];
            }
            else if (line == "--- end ---" && block is not null)
            {
                yield return string.Join('\n', block);
                block = null;
            }
            else
            {
                block?.Add(line);
            }
        }
    }

    private static string Route(ParsedContract parsed) =>
        string.Join(", ", parsed.Deliveries.Select(d => $"{DestinationNames.Key(d.Destination)}:{ContractText.AnchorForm(d.Commodity)}").Distinct().Order(StringComparer.Ordinal))
        + $" | {parsed.Reward}";

    private sealed record Scan(int No, string Text, ScannedContract Contract, ParsedContract Parsed);

    [GeneratedRegex(@"\sraw: \d+$")]
    private static partial Regex RawStart { get; }

    /// <summary>A line with a delivery's count ("Deliver O/115 SCU of …"), read or not.</summary>
    [GeneratedRegex(@"^\S+\s+\S*/\s*\d+\s+SCU\s+of\s", RegexOptions.IgnoreCase)]
    private static partial Regex DeliveryLike { get; }
}
