using CargoPlan.Core.Models;
using static CargoPlan.Core.Json.EnumWords;

namespace CargoPlan.Core.Routing;

/// <summary>The route as routes.json writes it (schema 3, T13.6, docs/routes-format.md).</summary>
internal sealed record RouteDto(
    string Name,
    DateTime StartedUtc,
    string Hub,
    string Kind,
    string System,
    IReadOnlyList<string> Stations,
    IReadOnlyList<PlaceholderDto> Placeholders,
    IReadOnlyList<string> Extras,
    int At,
    DateTime? DepartedUtc = null)
{
    public static RouteDto From(Route r) => new(
        r.Name, r.StartedUtc, r.Hub, Word(r.Kind), Word(r.System), r.Stations, [.. r.Placeholders.Select(PlaceholderDto.From)], r.Extras, r.At, r.DepartedUtc);

    /// <exception cref="ArgumentException">A word, time, index or placeholder out of the format.</exception>
    public Route ToRoute()
    {
        Utc(StartedUtc, "startedUtc");
        if (DepartedUtc is { } departed)
        {
            Utc(departed, "departedUtc");
            if (departed < StartedUtc)
            {
                throw new ArgumentException($"the route's departedUtc {departed:O} is before its startedUtc {StartedUtc:O}.");
            }
        }

        if (Stations.Count == 0)
        {
            throw new ArgumentException("the route has no stations.");
        }

        // 0 the hub at the start, 1 to n the stations, n + 1 the hub at the end.
        if (At < 0 || At > Stations.Count + 1)
        {
            throw new ArgumentException($"the route's at {At} is no stop of its {Stations.Count} stations and the hub.");
        }

        Placeholder[] placeholders = [.. Placeholders.Select(p => p.ToPlaceholder())];
        if (placeholders.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException($"placeholder id {twice.Key} is there twice.");
        }

        return new(Name, StartedUtc, Hub, Parse<LoopKind>(Kind), Parse<StarSystem>(System), Stations, placeholders, Extras, DepartedUtc, At);
    }

    internal static void Utc(DateTime time, string field)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"{field} {time:O} must be a UTC time.");
        }
    }
}

/// <summary>One placeholder; reward, accept, collect and filled left out where there are none.</summary>
internal sealed record PlaceholderDto(
    int Id,
    string Kind,
    string Template,
    string Title,
    IReadOnlyList<PlaceholderLegDto> Legs,
    IReadOnlyList<string> Destinations,
    long? Reward = null,
    string? AcceptAt = null,
    IReadOnlyList<string>? CollectAt = null,
    string? FilledBy = null)
{
    public static PlaceholderDto From(Placeholder p) => new(
        p.Id, Word(p.Kind), p.TemplateUuid, p.Title, [.. p.Legs.Select(l => new PlaceholderLegDto(l.Commodity, l.MinScu, l.MaxScu, (int)l.Container))],
        p.Destinations, p.Reward, p.AcceptAt, p.CollectAt, p.FilledBy);

    public Placeholder ToPlaceholder()
    {
        var kind = Parse<RecipeKind>(Kind);
        if (Id < 1 || Legs.Count == 0 || (Reward is { } reward && reward < 1))
        {
            throw new ArgumentException($"placeholder {Id} needs an id of at least 1, a leg and a reward of at least 1 where there is one.");
        }

        // A way back is accepted at a station and collects where it says (none for one pickup); no other role is.
        if ((kind == RecipeKind.Return) != (AcceptAt is not null) || (AcceptAt is null) != (CollectAt is null))
        {
            throw new ArgumentException($"placeholder {Id} ({Kind}): only a way back has acceptAt and collectAt, and it has both.");
        }

        return new(Id, kind, Template, Title, Reward, [.. Legs.Select(l => l.ToLeg(Id))], Destinations, AcceptAt, CollectAt, FilledBy);
    }
}

/// <summary>A placeholder's leg: its band and its largest crate in SCU.</summary>
internal sealed record PlaceholderLegDto(string Commodity, int Min, int Max, int Container)
{
    public HaulLeg ToLeg(int placeholder)
    {
        if (Min < 1 || Min > Max)
        {
            throw new ArgumentException($"placeholder {placeholder}: the band of {Commodity} ({Min}–{Max} SCU) is no band.");
        }

        return Enum.IsDefined((CrateSize)Container)
            ? new(Commodity, Min, Max, (CrateSize)Container)
            : throw new ArgumentException($"placeholder {placeholder}: container {Container} is no crate size.");
    }
}

/// <summary>A finished route (D126).</summary>
internal sealed record RunDto(
    string Name,
    DateTime StartedUtc,
    DateTime FinishedUtc,
    string Hub,
    string Kind,
    string System,
    int Landings,
    int Crates,
    int LongJumps,
    long Reward,
    int Filled,
    int Total,
    int Extras,
    string? Ship = null,
    IReadOnlyList<string>? Stations = null,
    DateTime? DepartedUtc = null,
    DirectDto? Direct = null)
{
    // Ship and stations: the loop's identity for measured times (T13.6c), written always, read where there; a direct
    // run's contract with them (P1).
    public static RunDto From(RouteRun r) => new(
        r.Name, r.StartedUtc, r.FinishedUtc, r.Hub, Word(r.Kind), Word(r.System), r.Landings, r.Crates, r.LongJumps, r.Reward, r.Filled, r.Total, r.Extras,
        r.ShipId, r.Stations, r.DepartedUtc, r.Direct is { } d ? new DirectDto(Word(d.Rank), Word(d.Size), d.Reward) : null);

    /// <exception cref="ArgumentException">A word, time or count out of the format, or a contract on a run that is no direct one.</exception>
    public RouteRun ToRun()
    {
        RouteDto.Utc(StartedUtc, "startedUtc");
        RouteDto.Utc(FinishedUtc, "finishedUtc");
        RouteDto.Utc(DepartedUtc ?? StartedUtc, "departedUtc");
        var from = DepartedUtc ?? StartedUtc;
        if (from < StartedUtc || FinishedUtc < from)
        {
            throw new ArgumentException($"the run {Name}: startedUtc, departedUtc and finishedUtc must follow in that order.");
        }

        if (Landings < 1 || Crates < 0 || LongJumps < 0 || Reward < 0 || Filled < 0 || Filled > Total || Extras < 0)
        {
            throw new ArgumentException($"the run {Name}: landings at least 1, no count below 0, filled at most total.");
        }

        var kind = Parse<LoopKind>(Kind);
        if (Direct is not null && kind != LoopKind.Direct)
        {
            throw new ArgumentException($"the run {Name}: only a direct run names its direct contract, this one is {Kind}.");
        }

        if (Direct?.Reward is < 1)
        {
            throw new ArgumentException($"the run {Name}: the direct contract's reward {Direct.Reward} is below 1.");
        }

        return new(Name, StartedUtc, DepartedUtc, FinishedUtc, Hub, kind, Parse<StarSystem>(System),
            Landings, Crates, LongJumps, Reward, Filled, Total, Extras, Ship, Stations,
            Direct is { } d ? new DirectContract(Parse<HaulRank>(d.Rank), Parse<HaulSize>(d.Size), d.Reward) : null);
    }
}

/// <summary>A direct run's outbound contract (T13.6c): words as in the hauling data, the reward left out where unknown.</summary>
internal sealed record DirectDto(string Rank, string Size, long? Reward = null);
