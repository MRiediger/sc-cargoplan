using static CargoPlan.Core.Json.EnumWords;

namespace CargoPlan.Core.Routing;

/// <summary>routes.json as written (docs/routes-format.md): the observations, since schema 3 (T13.6) the route and the runs, each left out while there is none.</summary>
internal sealed record RoutesFileDto(int SchemaVersion, IReadOnlyList<ObservationDto> Observations, RouteDto? Route = null, IReadOnlyList<RunDto>? Runs = null)
{
    public static RoutesFileDto From(Observations observations, Route? route, IReadOnlyList<RouteRun> runs) => new(
        RoutesFile.SupportedSchemaVersion, [.. observations.Entries.Select(ObservationDto.From)],
        route is null ? null : RouteDto.From(route), runs.Count == 0 ? null : [.. runs.Select(RunDto.From)]);

    /// <summary>The route and the runs; none before schema 3.</summary>
    /// <exception cref="ArgumentException">A route or run in an older schema, out of the format, or more runs than kept.</exception>
    public (Route? Route, IReadOnlyList<RouteRun> Runs) ToRoute(int schemaVersion)
    {
        if (schemaVersion < 3 && (Route is not null || Runs is not null))
        {
            throw new ArgumentException($"a route and runs need schema 3, the file says {schemaVersion}.");
        }

        if (Runs is { Count: > RouteRun.MaxKept })
        {
            throw new ArgumentException($"{Runs.Count} runs, at most {RouteRun.MaxKept} are kept.");
        }

        return (Route?.ToRoute(), Runs is null ? [] : [.. Runs.Select(r => r.ToRun())]);
    }

    /// <summary>The observations; of schema 1 only the entries with one commodity (K1 of T13.3), the others named.</summary>
    /// <exception cref="ArgumentException">A word, band, count or time out of the format, or a key twice.</exception>
    public (Observations Observations, IReadOnlyList<string> Dropped) ToObservations(int schemaVersion)
    {
        Observation[] all = [.. Observations.Select(o => o.ToObservation())];
        if (all.GroupBy(e => e.Key).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException($"the observation {twice.Key} is there twice.");
        }

        var keyedWithoutCommodities = schemaVersion < 2;
        string[] dropped =
        [
            .. all.Where(e => keyedWithoutCommodities && e.Scu.Count > 1)
                .Select(e => $"schema 1 entry {Word(e.Key.Contractor)} {Word(e.Key.Rank)} {Word(e.Key.Size)} {e.Key.Reward} left out: {e.Scu.Count} commodities may be several templates"),
        ];
        return (new([.. all.Where(e => !keyedWithoutCommodities || e.Scu.Count <= 1)]), dropped);
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

        // The key's commodities are those of the bands (K1 of T13.3): the file names each commodity once.
        var key = new ObservationKey(
            Parse<HaulContractor>(Contractor), Parse<HaulRank>(Rank), Parse<HaulSize>(Size), Direct, Reward, [.. Scu.Select(b => b.Commodity)]);
        return new(key, Area is null ? null : Parse<AreaLevel>(Area), Pickups, Destinations,
            [.. Scu.Select(b => new ScuBand(b.Commodity, b.Min, b.Max))], Count, FirstUtc, LastUtc);
    }
}

/// <summary>The SCU band of one commodity.</summary>
internal sealed record ScuBandDto(string Commodity, int Min, int Max);
