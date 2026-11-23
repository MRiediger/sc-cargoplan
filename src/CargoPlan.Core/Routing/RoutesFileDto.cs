using System.Text.Json;

namespace CargoPlan.Core.Routing;

/// <summary>routes.json as written (docs/routes-format.md). The route and its placeholders come later (T13.6).</summary>
internal sealed record RoutesFileDto(int SchemaVersion, IReadOnlyList<ObservationDto> Observations)
{
    public static RoutesFileDto From(Observations observations) =>
        new(RoutesFile.SupportedSchemaVersion, [.. observations.Entries.Select(ObservationDto.From)]);

    /// <exception cref="ArgumentException">A word, band, count or time out of the format, or a key twice.</exception>
    public Observations ToObservations()
    {
        Observation[] entries = [.. Observations.Select(o => o.ToObservation())];
        if (entries.GroupBy(e => e.Key).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException($"the observation {twice.Key} is there twice.");
        }

        return new(entries);
    }
}

/// <summary>One observation; <c>direct</c> and <c>area</c> left out where unknown.</summary>
internal sealed record ObservationDto(
    string Contractor,
    string Rank,
    string Size,
    long Reward,
    IReadOnlyList<string> Pickups,
    IReadOnlyList<string> Destinations,
    IReadOnlyList<ScuBandDto> Scu,
    int Count,
    DateTime FirstUtc,
    DateTime LastUtc,
    bool? Direct = null,
    string? Area = null)
{
    public static ObservationDto From(Observation o) => new(
        Word(o.Key.Contractor), Word(o.Key.Rank), Word(o.Key.Size), o.Key.Reward, o.Pickups, o.Destinations,
        [.. o.Scu.Select(b => new ScuBandDto(b.Commodity, b.Min, b.Max))], o.Count, o.FirstUtc, o.LastUtc,
        o.Key.Direct, o.Area is { } area ? Word(area) : null);

    public Observation ToObservation()
    {
        if (Reward <= 0 || Count < 1)
        {
            throw new ArgumentException($"reward {Reward} and count {Count} must be at least 1.");
        }

        if (FirstUtc.Kind != DateTimeKind.Utc || LastUtc.Kind != DateTimeKind.Utc || FirstUtc > LastUtc)
        {
            throw new ArgumentException($"firstUtc {FirstUtc:O} and lastUtc {LastUtc:O} must be UTC times in that order.");
        }

        if (Scu.FirstOrDefault(b => b.Min < 1 || b.Min > b.Max) is { } band)
        {
            throw new ArgumentException($"the band of {band.Commodity} ({band.Min}–{band.Max} SCU) is no band.");
        }

        var key = new ObservationKey(Parse<HaulContractor>(Contractor), Parse<HaulRank>(Rank), Parse<HaulSize>(Size), Direct, Reward);
        return new(key, Area is null ? null : Parse<AreaLevel>(Area), Pickups, Destinations,
            [.. Scu.Select(b => new ScuBand(b.Commodity, b.Min, b.Max))], Count, FirstUtc, LastUtc);
    }

    /// <summary>
    /// An enum value as the files spell it, camelCase as in docs/hauling-data-format.md ("xs", "redWind"); a test pins
    /// the words, so a renamed member shows.
    /// </summary>
    private static string Word<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static T Parse<T>(string word) where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            if (Word(value) == word)
            {
                return value;
            }
        }

        throw new ArgumentException($"{typeof(T).Name} '{word}' is unknown, use {string.Join(", ", Enum.GetValues<T>().Select(v => $"\"{Word(v)}\""))}.");
    }
}

/// <summary>The SCU band of one commodity.</summary>
internal sealed record ScuBandDto(string Commodity, int Min, int Max);
