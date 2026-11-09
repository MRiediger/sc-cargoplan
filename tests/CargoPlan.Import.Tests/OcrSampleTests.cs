using System.Globalization;
using System.Text;
using System.Text.Json;
using CargoPlan.Import.ContractTracker;
using CargoPlan.Ocr;
using SkiaSharp;

namespace CargoPlan.Import.Tests;

/// <summary>
/// By hand only (T10.1a): the real captures of S10.0 through the whole offline path (region, up2-gray, OCR, reading
/// order) and the existing parser, counted against the truth Martin and Claude read off the pictures. The captures are
/// CIG material outside the repo (CARGOPLAN_OCR_SAMPLES = the sc-ocr-samples folder); the report goes to
/// CARGOPLAN_OCR_OUT. See CLAUDE.md, Befehle.
/// </summary>
[Trait("Category", "Slow")]
public sealed class OcrSampleTests
{
    private static readonly string[] Sets = ["set", "set-1080s"];

    // S10.0: c06 of the 1080p set was captured while the game was still typing the objectives in ("Deliver 0/9 SCU of
    // Silicon" without a destination). Not an OCR fault; T10.2 handles it with a second read.
    private static readonly HashSet<string> TypingIn = ["set-1080s/c06"];

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Every_delivery_field_is_read_except_while_typing_in()
    {
        var samples = Required("CARGOPLAN_OCR_SAMPLES");
        var output = Required("CARGOPLAN_OCR_OUT");
        Directory.CreateDirectory(output);

        using var engine = OcrEngine.Load();
        engine.Warmup();
        var source = new ScanContractSource(engine);

        var report = new StringBuilder("| Set | Contract | View | Delivery fields | Max size | Reward | ms |\n|---|---|---|---|---|---|---|\n");
        var missed = new List<string>();
        foreach (var set in Sets)
        {
            var truth = JsonSerializer.Deserialize<Truth>(File.ReadAllText(Path.Combine(samples, set, "truth.json")), Json)!;
            var contracts = truth.Contracts.Where(c => c.Deliveries.Length > 0).ToDictionary(c => c.Id);
            var fields = 0;
            var read = 0;
            List<double> times = [];
            foreach (var sample in truth.Samples.Where(s => contracts.ContainsKey(s.Contract)))
            {
                var expected = contracts[sample.Contract];
                using var picture = SKBitmap.Decode(Path.Combine(samples, set, sample.File));
                var result = source.Read(picture);
                times.Add(result.Timings.Total.TotalMilliseconds);
                File.WriteAllText(Path.Combine(output, $"{set}_{sample.Contract}.txt"), result.RawText);

                var parsed = ContractTextParser.Parse(result.RawText);
                var hits = DeliveryFields(expected.Deliveries, parsed.Deliveries);
                fields += 3 * expected.Deliveries.Length;
                read += hits;
                if (hits < 3 * expected.Deliveries.Length && !TypingIn.Contains($"{set}/{sample.Contract}"))
                {
                    missed.Add($"{set}/{sample.Contract}: {hits} of {3 * expected.Deliveries.Length}");
                }

                report.Append(CultureInfo.InvariantCulture,
                    $"| {set} | {sample.Contract} | {sample.View} | {hits}/{3 * expected.Deliveries.Length} | {Mark((int?)parsed.MaxCrateSize, expected.MaxCrateSize)} | {Mark(parsed.Reward, expected.Reward)} | {result.Timings.Total.TotalMilliseconds:0} |\n");
            }

            times.Sort();
            report.Append(CultureInfo.InvariantCulture,
                $"| **{set}** | {times.Count} pictures | | **{read}/{fields} = {100.0 * read / fields:0.0} %** | | | median {times[times.Count / 2]:0}, p95 {times[(int)Math.Ceiling(0.95 * times.Count) - 1]:0} |\n");
        }

        File.WriteAllText(Path.Combine(output, "ocr-samples.md"), report.ToString());
        missed.Should().BeEmpty("every destination, commodity and SCU is read except in c06 of the 1080p set (S10.0); report in {0}", output);
    }

    // Pairs expected and parsed deliveries best first (most agreeing fields), each used once; 3 fields per delivery. Going
    // through the expected ones in order instead would let a missing delivery take another one's parsed partner.
    private static int DeliveryFields(TruthDelivery[] expected, IReadOnlyList<ParsedDelivery> parsed)
    {
        var pairs = from e in Enumerable.Range(0, expected.Length)
                    from p in Enumerable.Range(0, parsed.Count)
                    let agreement = Agreement(expected[e], parsed[p])
                    where agreement > 0
                    orderby agreement descending
                    select (e, p, agreement);
        HashSet<int> usedExpected = [];
        HashSet<int> usedParsed = [];
        var hits = 0;
        foreach (var (e, p, agreement) in pairs)
        {
            if (!usedExpected.Contains(e) && !usedParsed.Contains(p))
            {
                usedExpected.Add(e);
                usedParsed.Add(p);
                hits += agreement;
            }
        }

        return hits;
    }

    // Destinations compare as the planner compares them (B1): one place with or without "Station". The truth writes
    // "High Course" where the objective line says "High Course Station".
    private static int Agreement(TruthDelivery expected, ParsedDelivery parsed) =>
        (DestinationNames.Comparer.Equals(ContractText.NormalizeDestination(expected.Destination), parsed.Destination) ? 1 : 0)
        + (Same(ContractText.NormalizeCommodity(expected.Commodity), parsed.Commodity) ? 1 : 0)
        + (expected.Scu == parsed.Scu ? 1 : 0);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // Reported, not asserted: the parser learns the second max-size wording and the reward in one line in T10.2.
    private static string Mark<T>(T? parsed, T? expected) where T : struct =>
        Equals(parsed, expected) ? "ok" : $"{parsed?.ToString() ?? "–"} ≠ {expected?.ToString() ?? "–"}";

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value
            : throw new InvalidOperationException($"{variable} is not set; this test runs by hand, see CLAUDE.md, Befehle.");

    private sealed record Truth(TruthContract[] Contracts, TruthSample[] Samples);

    private sealed record TruthContract(string Id, TruthDelivery[] Deliveries, int? MaxCrateSize, long? Reward);

    private sealed record TruthDelivery(string Destination, string Commodity, int Scu);

    private sealed record TruthSample(string File, string Contract, string View);
}
