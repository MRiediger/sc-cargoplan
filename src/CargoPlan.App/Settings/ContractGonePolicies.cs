using CargoPlan.Core.Planning;

namespace CargoPlan.App.Settings;

/// <summary>
/// <see cref="ContractGonePolicy"/> as settings.json writes it, "keep" or "remove" (<c>contractTracker.onContractGone</c>,
/// D81); a string in the file like <see cref="ShipOutlineModes"/>, so an unknown name keeps with a notice
/// (<see cref="AppearanceRules"/>) instead of making the file invalid (D13).
/// </summary>
public static class ContractGonePolicies
{
    private static readonly (ContractGonePolicy Policy, string Name)[] Table =
    [
        (ContractGonePolicy.Keep, "keep"),
        (ContractGonePolicy.Remove, "remove"),
    ];

    /// <summary>What a missing key means: keep (D81).</summary>
    public const string DefaultName = "keep";

    /// <summary>Every valid name, in the enum's order; settings-format.md lists the same.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. Table.Select(t => t.Name)];

    /// <summary>Exactly one of <see cref="Names"/>, lower case like the keys; not a number, unlike <c>Enum.TryParse</c>.</summary>
    public static bool TryParse(string text, out ContractGonePolicy policy)
    {
        var index = Array.FindIndex(Table, t => t.Name == text);
        policy = index >= 0 ? Table[index].Policy : default;
        return index >= 0;
    }

    /// <exception cref="FormatException">Not a policy; after <see cref="AppearanceRules.NormalizeContractTracker"/> that is a bug.</exception>
    public static ContractGonePolicy Parse(string text) =>
        TryParse(text, out var policy) ? policy : throw new FormatException($"\"{text}\" is not a policy for gone contracts ({string.Join(", ", Names)}).");

    public static string ToName(ContractGonePolicy policy) => Table.Single(t => t.Policy == policy).Name;
}
