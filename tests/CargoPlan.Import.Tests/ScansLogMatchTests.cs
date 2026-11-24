using System.Globalization;
using System.Text;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Import.Contracts;
using Xunit.Abstractions;

namespace CargoPlan.Import.Tests;

/// <summary>
/// By hand only (T13.3, D122): every raw text of a scans.log (D103, the overlay's last 50 scans) as the overlay would
/// observe it – parsed, recorded, folded – and each observation matched against the pinned release. The log is the
/// player's and CIG material outside the repo (CARGOPLAN_SCANS_LOG); the numbers go to the report and TASKS.md, the only
/// assert is that the log held a scan. See CLAUDE.md, Befehle.
/// </summary>
[Trait("Category", "Slow")]
public sealed class ScansLogMatchTests(ITestOutputHelper output)
{
    private const string EntryStart = "=== ";

    [Fact]
    public void Every_observation_of_a_scans_log_is_matched_or_names_why_not()
    {
        var path = Environment.GetEnvironmentVariable("CARGOPLAN_SCANS_LOG") is { Length: > 0 } value ? value
            : throw new InvalidOperationException("CARGOPLAN_SCANS_LOG is not set; this test runs by hand, see CLAUDE.md, Befehle.");
        var data = HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder)).Should().BeOfType<HaulingLoad.Loaded>().Which.Data;

        var texts = RawTexts(File.ReadAllLines(path)).ToList();
        var scans = texts.Select(t => ScannedContract.Read(t).ToObservation(DateTime.UtcNow)).ToList();
        var observations = scans.OfType<ObservedScan>().Aggregate(Observations.Empty, (all, scan) => all.Record(scan));
        var perScan = scans.OfType<ObservedScan>().Select(s => TemplateMatch.Find(Observation.Of(s), data.Templates, data.Positions)).ToList();
        var perEntry = observations.Entries.Select(e => (Entry: e, Result: TemplateMatch.Find(e, data.Templates, data.Positions))).ToList();

        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"{texts.Count} scans, {perScan.Count} observations, {perEntry.Count} entries after recording");
        report.AppendLine(CultureInfo.InvariantCulture, $"per scan:  {Quota(perScan)}");
        report.AppendLine(CultureInfo.InvariantCulture, $"per entry: {Quota(perEntry.Select(e => e.Result).ToList())}");
        foreach (var (entry, result) in perEntry)
        {
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  {entry.Count,3} × {entry.Key.Contractor} {entry.Key.Rank} {entry.Key.Size} {entry.Key.Reward,9:N0} {string.Join("+", entry.Scu.Select(b => b.Commodity))}: {Describe(result)}");
        }

        output.WriteLine(report.ToString());
        texts.Should().NotBeEmpty();
    }

    private static string Quota(IReadOnlyList<MatchResult> results) =>
        $"{results.Count(r => r is MatchResult.Matched)} matched, {results.Count(r => r is MatchResult.Ambiguous)} ambiguous, {results.Count(r => r is MatchResult.None)} none of {results.Count}";

    private static string Describe(MatchResult result) => result switch
    {
        MatchResult.Matched m => $"matched {m.Template.DebugName}",
        MatchResult.Ambiguous a => $"ambiguous ({a.Candidates.Count}) {string.Join(", ", a.Candidates.Select(c => c.DebugName))}",
        MatchResult.None n => $"none: {n.Reason}",
        _ => throw new ArgumentOutOfRangeException(nameof(result)),
    };

    /// <summary>The raw text under each "=== &lt;UTC&gt; &lt;result&gt;" line (ScanLog's format, D103).</summary>
    private static IEnumerable<string> RawTexts(IEnumerable<string> lines)
    {
        List<string>? block = null;
        foreach (var line in lines)
        {
            if (line.StartsWith(EntryStart, StringComparison.Ordinal))
            {
                if (block is [_, ..])
                {
                    yield return string.Join('\n', block);
                }

                block = [];
            }
            else
            {
                block?.Add(line);
            }
        }

        if (block is [_, ..])
        {
            yield return string.Join('\n', block);
        }
    }
}
