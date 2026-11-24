using CargoPlan.Core.Text;

namespace CargoPlan.Core.Routing;

/// <summary>A place of positions.json: its starmap name and where it lies, in metres, in its system's coordinates.</summary>
/// <param name="Parent">The <see cref="Name"/> of its parent position (a station's planet) when the file has it.</param>
/// <param name="ContractNames">Other forms contracts use for it (K1 of T13.0a); the scan's match (T13.3) reads them.</param>
public sealed record StarPosition(
    string Uuid,
    string Name,
    StarSystem System,
    string Type,
    string? Parent,
    double X,
    double Y,
    double Z,
    IReadOnlyList<string> ContractNames);

/// <summary>Every position of the data release; a name is unique per system, not per file (K3: the gateways).</summary>
public sealed class Positions
{
    private readonly Dictionary<(StarSystem, string), StarPosition> _byName;

    // B1 key → the one position it names; null where two positions share a key (resolved to none, never guessed).
    private readonly Dictionary<(StarSystem, string), StarPosition?> _byKey = [];

    /// <exception cref="ArgumentException">A name twice in one system.</exception>
    public Positions(IReadOnlyList<StarPosition> all)
    {
        All = all;
        _byName = [];
        foreach (var position in all)
        {
            if (!_byName.TryAdd((position.System, position.Name), position))
            {
                throw new ArgumentException($"'{position.Name}' is there twice in {position.System}.");
            }

            foreach (var key in KeysOf(position))
            {
                if (!_byKey.TryAdd((position.System, key), position))
                {
                    _byKey[(position.System, key)] = null;
                }
            }
        }
    }

    public static Positions Empty { get; } = new([]);

    public IReadOnlyList<StarPosition> All { get; }

    public StarPosition? Find(StarSystem system, string name) => _byName.GetValueOrDefault((system, name));

    /// <summary>
    /// The position a scan names (T13.3, hauling-data-format.md "Names"): by the plan's name rule (B1,
    /// <see cref="DestinationNames.Key"/>, so OCR forms and a missing "Station" alike) against <see cref="KeysOf"/>.
    /// Null for a name no position of <paramref name="system"/> answers to, or one two of them do.
    /// </summary>
    public StarPosition? Resolve(StarSystem system, string scannedName) => _byKey.GetValueOrDefault((system, DestinationNames.Key(scannedName)));

    /// <summary>
    /// The B1 keys a contract may name <paramref name="position"/> by: its starmap name, the name without its L code (the
    /// card writes deliveries to "Green Glade Station", not "HUR-L1 Green Glade Station": a fact of the card, not of the
    /// data, so it lives here, K1 of T13.0a), and every contract name of the data (the Pyro long forms).
    /// </summary>
    public static IReadOnlyList<string> KeysOf(StarPosition position) =>
    [
        .. new[] { position.Name, PlaceNames.WithoutCode(position.Name) }.Concat(position.ContractNames)
            .Select(DestinationNames.Key)
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>Whether <paramref name="name"/> is a place of any system (a template without a system).</summary>
    public bool AnySystemHas(string name) => Enum.GetValues<StarSystem>().Any(s => _byName.ContainsKey((s, name)));

    /// <summary>The distance in Gm (10⁹ m), Euclidean; null across systems, where it is not defined.</summary>
    public static double? DistanceGm(StarPosition a, StarPosition b) => a.System != b.System
        ? null
        : Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2)) / 1e9;
}
