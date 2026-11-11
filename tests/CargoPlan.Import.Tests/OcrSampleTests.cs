using System.Globalization;
using System.Text;
using System.Text.Json;
using CargoPlan.Import.Contracts;
using CargoPlan.Ocr;
using SkiaSharp;

namespace CargoPlan.Import.Tests;

/// <summary>
/// By hand only (T10.1a, T10.2a): the real captures of S10.0 through the whole offline path (region, up2-gray, OCR,
/// reading order) and the parser, counted against the truth Martin and Claude read off the pictures: every delivery
/// field, the pickups, max size, reward and view, and the content key (a contract and its pair after accepting share one,
/// two contracts never). The captures are CIG material outside the repo (CARGOPLAN_OCR_SAMPLES = the sc-ocr-samples
/// folder); the report goes to CARGOPLAN_OCR_OUT. See CLAUDE.md, Befehle.
/// </summary>
[Trait("Category", "Slow")]
public sealed class OcrSampleTests
{
    private static readonly string[] Sets = ["set", "set-1080s"];

    // S10.0: c06 of the 1080p set was captured while the game was still typing the objectives in ("Deliver 0/9 SCU of
    // Silicon" without a destination). Not an OCR fault; T10.2b handles it with a second read on suspicion.
    private static readonly HashSet<string> TypingIn = ["set-1080s/c06"];

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Every_field_is_read_and_each_pair_has_one_key_except_while_typing_in()
    {
        var samples = Required("CARGOPLAN_OCR_SAMPLES");
        var output = Required("CARGOPLAN_OCR_OUT");
        Directory.CreateDirectory(output);

        using var engine = OcrEngine.Load();
        engine.Warmup();
        var source = new ScanContractSource(engine);

        var report = new StringBuilder(
            "| Set | Contract | View | Delivery fields | Pickup | Max size | Reward | Doubts | Key | ms |\n|---|---|---|---|---|---|---|---|---|---|\n");
        var missed = new List<string>();
        foreach (var set in Sets)
        {
            var truth = JsonSerializer.Deserialize<Truth>(File.ReadAllText(Path.Combine(samples, set, "truth.json")), Json)!;
            var contracts = truth.Contracts.Where(c => c.Deliveries.Length > 0).ToDictionary(c => c.Id);
            Totals totals = new();
            List<double> times = [];
            Dictionary<string, string> keys = [];
            foreach (var sample in truth.Samples.Where(s => contracts.ContainsKey(s.Contract)))
            {
                var expected = contracts[sample.Contract];
                using var picture = SKBitmap.Decode(Path.Combine(samples, set, sample.File));
                var result = source.Read(picture);
                times.Add(result.Timings.Total.TotalMilliseconds);
                File.WriteAllText(Path.Combine(output, $"{set}_{sample.Contract}.txt"), result.RawText);

                var parsed = ContractTextParser.Parse(result.RawText);
                var scanned = ScannedContract.Read(result.RawText);
                var typingIn = TypingIn.Contains($"{set}/{sample.Contract}");
                if (!typingIn)
                {
                    keys[sample.Contract] = scanned.Key;
                }

                // A second read only for the capture typing in (T10.2b2, D103); a soft doubt is reported, not asserted.
                if (scanned.InHardDoubt != typingIn)
                {
                    missed.Add($"{set}/{sample.Contract}: hard doubt {scanned.InHardDoubt} ({string.Join(", ", scanned.Doubts)})");
                }

                var hits = DeliveryFields(expected.Deliveries, parsed.Deliveries);
                var fields = 3 * expected.Deliveries.Length;
                Check check = new(
                    hits == fields,
                    SamePlaces(expected.Pickup, scanned.Pickups),
                    (int?)parsed.MaxCrateSize == expected.MaxCrateSize,
                    scanned.Reward == expected.Reward,
                    scanned.View == ViewOf(sample.View));
                totals = totals.Add(hits, fields, check);
                if (!check.All && !typingIn)
                {
                    missed.Add($"{set}/{sample.Contract}: {check}");
                }

                report.Append(CultureInfo.InvariantCulture,
                    $"| {set} | {sample.Contract} | {sample.View} → {scanned.View} | {hits}/{fields} | {Mark(check.Pickup, Places(scanned.Pickups), Places(expected.Pickup))} | {Mark((int?)parsed.MaxCrateSize, expected.MaxCrateSize)} | {Mark(scanned.Reward, expected.Reward)} | {(scanned.Doubts.Count == 0 ? "–" : string.Join(", ", scanned.Doubts))} | `{scanned.Key}` | {result.Timings.Total.TotalMilliseconds:0} |\n");
            }

            var keyProblems = KeyProblems(contracts.Values, keys);
            missed.AddRange(keyProblems.Select(p => $"{set}: {p}"));
            times.Sort();
            report.Append(CultureInfo.InvariantCulture,
                $"| **{set}** | {times.Count} pictures | {totals.Views} views | **{totals.Read}/{totals.Fields} = {100.0 * totals.Read / totals.Fields:0.0} %** | {totals.Pickups} | {totals.Max} | {totals.Rewards} | | {keys.Values.Distinct().Count()} keys, {keyProblems.Count} problems | median {times[times.Count / 2]:0}, p95 {times[(int)Math.Ceiling(0.95 * times.Count) - 1]:0} |\n");
        }

        File.WriteAllText(Path.Combine(output, "ocr-samples.md"), report.ToString());
        missed.Should().BeEmpty("every field is read and every pair has one key except c06 of the 1080p set (S10.0); report in {0}", output);
    }

    /// <summary>A contract and its pair share a key; any two other contracts don't (D101). Contracts while typing in are left out.</summary>
    private static List<string> KeyProblems(IEnumerable<TruthContract> contracts, Dictionary<string, string> keys)
    {
        List<string> problems = [];
        foreach (var contract in contracts.Where(c => keys.ContainsKey(c.Id)))
        {
            if (contract.Pair is { } pair && keys.TryGetValue(pair, out var pairKey) && pairKey != keys[contract.Id])
            {
                problems.Add($"{contract.Id} and its pair {pair} have two keys");
            }

            foreach (var other in keys.Where(k => k.Key != contract.Id && k.Key != contract.Pair && k.Value == keys[contract.Id]))
            {
                problems.Add($"{contract.Id} and {other.Key} share a key");
            }
        }

        return problems;
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

    /// <summary>The same places, each compared as one place is (B1), order and repeats aside.</summary>
    private static bool SamePlaces(IReadOnlyList<string> expected, IReadOnlyList<string> read) =>
        expected.Select(DestinationNames.Key).ToHashSet(StringComparer.Ordinal).SetEquals(read.Select(DestinationNames.Key));

    private static ContractView ViewOf(string truthView) => truthView switch
    {
        "terminal" => ContractView.Offer,
        "manager" => ContractView.Accepted,
        _ => throw new InvalidOperationException($"Unknown view '{truthView}' in truth.json."),
    };

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Places(IReadOnlyList<string> places) => places.Count == 0 ? "–" : string.Join(" / ", places);

    private static string Mark(bool ok, string read, string expected) => ok ? "ok" : $"{read} ≠ {expected}";

    private static string Mark<T>(T? parsed, T? expected) where T : struct =>
        Equals(parsed, expected) ? "ok" : $"{parsed?.ToString() ?? "–"} ≠ {expected?.ToString() ?? "–"}";

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value
            : throw new InvalidOperationException($"{variable} is not set; this test runs by hand, see CLAUDE.md, Befehle.");

    private sealed record Check(bool Deliveries, bool Pickup, bool Max, bool Reward, bool View)
    {
        public bool All => Deliveries && Pickup && Max && Reward && View;
    }

    private sealed record Totals(int Read = 0, int Fields = 0, int Pickups = 0, int Max = 0, int Rewards = 0, int Views = 0)
    {
        public Totals Add(int read, int fields, Check check) => new(
            Read + read, Fields + fields, Pickups + (check.Pickup ? 1 : 0), Max + (check.Max ? 1 : 0),
            Rewards + (check.Reward ? 1 : 0), Views + (check.View ? 1 : 0));
    }

    private sealed record Truth(TruthContract[] Contracts, TruthSample[] Samples);

    private sealed record TruthContract(string Id, TruthDelivery[] Deliveries, int? MaxCrateSize, long? Reward, string[] Pickup, string? Pair);

    private sealed record TruthDelivery(string Destination, string Commodity, int Scu);

    private sealed record TruthSample(string File, string Contract, string View);
}
