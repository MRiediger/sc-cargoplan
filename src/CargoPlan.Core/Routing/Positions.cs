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
        }
    }

    public static Positions Empty { get; } = new([]);

    public IReadOnlyList<StarPosition> All { get; }

    public StarPosition? Find(StarSystem system, string name) => _byName.GetValueOrDefault((system, name));

    /// <summary>Whether <paramref name="name"/> is a place of any system (a template without a system).</summary>
    public bool AnySystemHas(string name) => Enum.GetValues<StarSystem>().Any(s => _byName.ContainsKey((s, name)));

    /// <summary>The distance in Gm (10⁹ m), Euclidean; null across systems, where it is not defined.</summary>
    public static double? DistanceGm(StarPosition a, StarPosition b) => a.System != b.System
        ? null
        : Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2)) / 1e9;
}
