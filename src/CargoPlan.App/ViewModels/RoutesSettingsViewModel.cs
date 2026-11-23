using CargoPlan.App.Settings;
using CargoPlan.Core.Json;
using CargoPlan.Core.Routing;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CargoPlan.App.ViewModels;

/// <summary>A choice of the Plan tab section: the word settings.json keeps, and what the list shows.</summary>
public sealed record RoutesChoice(string Value, string Text);

/// <summary>
/// The settings window's section of the Plan tab (T13.3): the player's rank and faction, set once instead of through the
/// tab's filter buttons, which write the same values (routes.rank, routes.faction). Applied at once through the settings
/// session (D30); its own DataContext like the Ship data section's, and it follows the session while the window is open,
/// so a filter button's change shows here too.
/// </summary>
public sealed partial class RoutesSettingsViewModel : ObservableObject
{
    /// <summary>The faction choice for "both sides"; settings.json writes null for it.</summary>
    public const string AnyFaction = "any";

    private readonly Action<string, string?> _change;

    private readonly Action<EventHandler<RoutesSettings>> _unfollow;

    // True while values come from the session: they are what it holds already, nothing to write back.
    private bool _following;

    /// <param name="change">Writes rank and faction (null = both) into the session's routes section.</param>
    /// <param name="follow">Subscribes to <see cref="SettingsSession.RoutesChanged"/>.</param>
    /// <param name="unfollow">Unsubscribes, from <see cref="Detach"/> when the window closes.</param>
    public RoutesSettingsViewModel(
        RoutesSettings current, Action<string, string?> change, Action<EventHandler<RoutesSettings>> follow, Action<EventHandler<RoutesSettings>> unfollow)
    {
        (_change, _unfollow) = (change, unfollow);
        Show(current);
        follow(OnSessionChanged);
    }

    public static IReadOnlyList<RoutesChoice> RankChoices { get; } =
        [.. Enum.GetValues<HaulRank>().Select(r => new RoutesChoice(EnumWords.Word(r), r.ToString()))];

    public static IReadOnlyList<RoutesChoice> FactionChoices { get; } =
    [
        new(AnyFaction, "Both sides"),
        new(EnumWords.Word(HaulFaction.Hh), "Headhunters"),
        new(EnumWords.Word(HaulFaction.Cfp), "Citizens for Prosperity"),
    ];

    /// <summary>The rank word ("senior"): the boards show every rank up to it.</summary>
    [ObservableProperty]
    public partial string Rank { get; set; } = RoutesSettings.AnyRank;

    /// <summary>The faction word, or <see cref="AnyFaction"/>; narrows Red Wind in Pyro only.</summary>
    [ObservableProperty]
    public partial string Faction { get; set; } = AnyFaction;

    /// <summary>The window closed: no more following the session.</summary>
    public void Detach() => _unfollow(OnSessionChanged);

    partial void OnRankChanged(string value) => Write();

    partial void OnFactionChanged(string value) => Write();

    private void Write()
    {
        if (!_following)
        {
            _change(Rank, Faction == AnyFaction ? null : Faction);
        }
    }

    private void OnSessionChanged(object? sender, RoutesSettings settings) => Show(settings);

    private void Show(RoutesSettings settings)
    {
        _following = true;
        Rank = settings.Rank;
        Faction = settings.Faction ?? AnyFaction;
        _following = false;
    }
}
