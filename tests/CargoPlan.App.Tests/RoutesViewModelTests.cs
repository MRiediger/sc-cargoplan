using CargoPlan.App.Routing;
using CargoPlan.App.Settings;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The Plan tab's view model (T13.2): the port matrix as rows and cells, the filter as buttons that cycle, rank and
/// faction saved in settings.json, a sentence instead of the matrix without hauling data. The matrix is Core's.
/// </summary>
public sealed class RoutesViewModelTests
{
    private static readonly ShipDataVersion Version = new(2026, 11, 2);

    private readonly List<RoutesSettings> _saved = [];

    private static HaulTemplate T(
        string[] pickups,
        string[] dropoffs,
        long? reward = 100_000,
        int min = 10,
        int max = 64,
        HaulRank rank = HaulRank.Senior,
        HaulContractor contractor = HaulContractor.Covalex,
        StarSystem? system = StarSystem.Stanton,
        string areaKey = "Stanton1",
        HaulMissionType type = HaulMissionType.Stellar,
        HaulFaction? faction = null,
        string title = "Senior Rank - Medium Cargo Haul") => new(
        "u", "d", contractor, title, rank, HaulSize.Medium, false, HaulShape.AToB, 1, system, areaKey, type, faction, reward,
        [new HaulLeg("Gold", min, max, CrateSize.Sixteen)], pickups, dropoffs);

    private RoutesViewModel Model(IReadOnlyList<HaulTemplate> templates, RoutesSettings? settings = null, ObservedEdges? observed = null, Positions? positions = null) =>
        new(new HaulingLoad.Loaded(new HaulingData(templates, positions ?? Positions.Empty), []), Version, settings ?? RoutesSettings.Default, _saved.Add, observed);

    private static readonly DateTime Monday = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Positions Places = new(
    [
        .. new[] { "Everus Harbor", "Baijini Point" }.Select(n => new StarPosition(n, n, StarSystem.Stanton, "Manmade", null, 0, 0, 0, [])),
        .. new[] { "P", "Q" }.Select(n => new StarPosition(n, n, StarSystem.Pyro, "Manmade", null, 0, 0, 0, [])),
    ]);

    // Seen twice: a Senior Medium for the reward, Gold, from the first place to the second.
    private static ObservedEdges Seen(HaulTemplate template, HaulContractor contractor, long reward, string from, string to)
    {
        var key = new ObservationKey(contractor, HaulRank.Senior, HaulSize.Medium, contractor == HaulContractor.Covalex ? false : null, reward, ["Gold"]);
        var scan = new ObservedScan(key, null, [from], [new ObservedDelivery(to, "Gold", 30)], Monday);
        return ObservedEdges.From(new Observations([Observation.Of(scan).With(scan)]), [template], Places);
    }

    private static PortCellView? Cell(RoutesViewModel model, string pickup, string dropoff) =>
        model.Rows.Single(r => r.Pickup == pickup).Cells[model.Columns.ToList().IndexOf(dropoff)];

    [Fact]
    public void Rows_are_pickups_columns_drop_offs_and_a_cell_reads_count_reward_and_scu()
    {
        var model = Model([T(["A"], ["B"], reward: 100_000, max: 64), T(["A"], ["B"], reward: 250_250, max: 144), T(["A"], ["C"])]);

        model.HasData.Should().BeTrue();
        model.StatusText.Should().BeNull();
        model.Columns.Should().Equal("B", "C");
        model.Rows.Select(r => r.Pickup).Should().Equal("A");
        Cell(model, "A", "B")!.Text.Should().Be("2 · 350k");
        Cell(model, "A", "C")!.Text.Should().Be("1 · 100k");
        model.Legend.Should().Be("Each cell: contracts · aUEC together; ● seen in your scans");
    }

    [Fact]
    public void A_cell_without_rewards_shows_a_dash_and_one_with_some_shows_a_plus()
    {
        var redWind = T(["P"], ["Q"], reward: null, max: 600, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh);
        var model = Model([redWind]);

        model.NextCommand.Execute("system");

        model.SystemLabel.Should().Be("Pyro");
        Cell(model, "P", "Q")!.Text.Should().Be("1 · –");
        RoutesViewModel.CellText(new PortCell([new(T(["A"], ["B"]), false, false), new(redWind, false, false)])).Should().Be("2 · 100k+");
    }

    [Theory]
    [InlineData(950L, "950")]
    [InlineData(57_000L, "57k")]
    [InlineData(482_250L, "482k")]
    [InlineData(1_234_567L, "1.2M")]
    public void Rewards_are_short(long reward, string text) => RoutesViewModel.Short(reward).Should().Be(text);

    [Fact]
    public void The_tool_tip_lists_the_templates_and_marks_the_boards_hub()
    {
        var model = Model([T([], ["HUR-L1"], reward: 160_250, min: 90, max: 144, title: "Experienced Rank - Direct Medium Cargo Haul")]);

        var tip = Cell(model, "Everus Harbor", "HUR-L1")!.ToolTip;

        tip.Should().StartWith("Everus Harbor → HUR-L1 · most SCU in one contract: 144");
        tip.Should().Contain("Experienced Rank - Direct Medium Cargo Haul · 160,250 aUEC · 90–144 SCU");
    }

    [Fact]
    public void Templates_at_the_boards_hub_carry_a_mark_the_head_explains_once()
    {
        // E2 of T13.3a: the hint once at the top, a short mark on each row it concerns, not the hint on every row.
        var model = Model(
        [
            T([], ["HUR-L1"], title: "Rookie Rank - Small Cargo Haul"),
            T(["Everus Harbor"], ["HUR-L1"], title: "Junior Rank - Small Cargo Haul"),
            T([], ["HUR-L1"], title: "Member Rank - Small Cargo Haul"),
            T(["HUR-L1"], [], title: "Senior Rank - Small Cargo Haul"),
        ]);

        Cell(model, "Everus Harbor", "HUR-L1")!.ToolTip.Split('\n').Should().Equal(
            "Everus Harbor → HUR-L1 · most SCU in one contract: 64",
            "⌂ pickup: the board's hub, not in the game data",
            "⌂ Rookie Rank - Small Cargo Haul · 100,000 aUEC · 10–64 SCU",
            "Junior Rank - Small Cargo Haul · 100,000 aUEC · 10–64 SCU",
            "⌂ Member Rank - Small Cargo Haul · 100,000 aUEC · 10–64 SCU");
        Cell(model, "HUR-L1", "Everus Harbor")!.ToolTip.Split('\n').Should().Equal(
            "HUR-L1 → Everus Harbor · most SCU in one contract: 64",
            "⌂ drop-off: the board's hub, not in the game data",
            "⌂ Senior Rank - Small Cargo Haul · 100,000 aUEC · 10–64 SCU");
        Model([T(["A"], ["B"])]).Rows.Single().Cells.Single()!.ToolTip.Should().NotContain("⌂", "no hub, no mark and no head line");
    }

    // Picture gate: L stations by their code, hubs in full; the full name stays in the row and the cell's tool tip.
    [Fact]
    public void L_stations_show_by_their_code_and_keep_their_full_name()
    {
        var model = Model([T(["HUR-L1 Green Glade Station"], ["Everus Harbor"]), T([], ["HUR-L3 Thundering Express Station"])]);

        model.Rows.Select(r => (r.Label, r.Pickup)).Should().Equal(("Everus Harbor", "Everus Harbor"), ("HUR-L1", "HUR-L1 Green Glade Station"));
        model.Columns.Should().Equal("Everus Harbor", "HUR-L3 Thundering Express Station");
        Cell(model, "Everus Harbor", "HUR-L3 Thundering Express Station")!.ToolTip.Should().StartWith("Everus Harbor → HUR-L3 Thundering Express Station");
    }

    [Fact]
    public void Templates_without_a_place_are_counted_below_the_matrix()
    {
        Model([T(["A"], [], areaKey: "Stanton"), T(["A"], ["B"])]).WithoutPlaceText
            .Should().Be("1 template not shown: the game data name no pickup or destination for it.");
        Model([T(["A"], [], areaKey: "Stanton"), T(["A"], [], areaKey: "Stanton")]).WithoutPlaceText
            .Should().Be("2 templates not shown: the game data name no pickup or destination for them.");
        Model([T(["A"], ["B"])]).WithoutPlaceText.Should().BeNull();
    }

    [Fact]
    public void A_pair_a_scan_saw_carries_a_mark_and_says_when_in_its_tool_tip()
    {
        // T13.3: a system-wide template without a place in the data gets its cell from the scans.
        var wide = T([], [], areaKey: "Stanton", title: "Senior Rank - Medium Cargo Haul") with { Uuid = "wide" };
        var model = Model([wide]);
        model.WithoutPlaceText.Should().NotBeNull();

        model.ShowObserved(Seen(wide, HaulContractor.Covalex, 100_000, "Everus Harbor", "Baijini Point"));

        var cell = Cell(model, "Everus Harbor", "Baijini Point")!;
        cell.Text.Should().Be("● 1 · 100k");
        cell.ToolTip.Should().Contain("Senior Rank - Medium Cargo Haul · 100,000 aUEC · 10–64 SCU · observed 2× · last 23 Nov");
        model.WithoutPlaceText.Should().BeNull("the template has a cell now");
    }

    [Fact]
    public void A_Red_Wind_cell_shows_the_reward_a_scan_saw()
    {
        var redWind = T(["P"], ["Q"], reward: null, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh) with { Uuid = "rw" };
        var model = Model([redWind], observed: Seen(redWind, HaulContractor.RedWind, 532_500, "P", "Q"));

        model.NextCommand.Execute("system");

        var cell = Cell(model, "P", "Q")!;
        cell.Text.Should().Be("● 1 · 532k");
        cell.ToolTip.Should().Contain("532,500 aUEC observed");
    }

    [Fact]
    public void Rank_and_faction_set_in_the_settings_window_show_in_the_filter_and_are_not_saved_again()
    {
        var model = Model([T(["A"], ["B"], rank: HaulRank.Senior)]);

        model.Follow(RoutesSettings.Default with { Rank = "junior", Faction = "cfp" });

        model.RankLabel.Should().Be("Rank ≤ Junior");
        model.FactionLabel.Should().Be("CFP");
        model.Rows.Should().BeEmpty("a Senior template is above the rank");
        _saved.Should().BeEmpty();
    }

    [Fact]
    public void Pyro_says_its_regional_templates_are_not_shown_yet()
    {
        var model = Model([T(["A"], ["B"])]);
        model.RegionsText.Should().BeNull();

        model.NextCommand.Execute("system");

        model.SystemLabel.Should().Be("Pyro");
        model.RegionsText.Should().Be("Red Wind's regional templates are not shown yet: no scan has placed them.");
    }

    [Fact]
    public void A_filter_that_lets_nothing_through_says_so()
    {
        var model = Model([T(["A"], ["B"])]);

        model.ShowMedium = false;

        model.Rows.Should().BeEmpty();
        model.EmptyText.Should().Be("No templates for this filter.");
    }

    [Fact]
    public void Rank_and_faction_start_from_the_settings_and_are_saved_on_every_change()
    {
        var model = Model([T(["A"], ["B"], rank: HaulRank.Junior), T(["A"], ["B"], rank: HaulRank.Master)], new RoutesSettings("plan", "senior", "cfp"));

        model.RankLabel.Should().Be("Rank ≤ Senior");
        model.FactionLabel.Should().Be("CFP");
        Cell(model, "A", "B")!.Text.Should().StartWith("1 ·");

        model.NextCommand.Execute("rank");
        model.RankLabel.Should().Be("Rank ≤ Master");
        Cell(model, "A", "B")!.Text.Should().StartWith("2 ·");
        model.PreviousCommand.Execute("faction");

        _saved.Should().Equal(new RoutesSettings("plan", "master", "cfp"), new RoutesSettings("plan", "master", "hh"));
    }

    [Fact]
    public void Every_filter_cycles_round_and_back()
    {
        var model = Model([T(["A"], ["B"])]);

        model.DirectLabel.Should().Be("Direct: any");
        model.NextCommand.Execute("direct");
        model.DirectLabel.Should().Be("Direct only");
        model.PreviousCommand.Execute("direct");
        model.PreviousCommand.Execute("direct");
        model.DirectLabel.Should().Be("No direct");

        model.ContractorLabel.Should().Be("Any contractor");
        model.AreaLabel.Should().Be("Stellar", "the matrix starts with stellar (picture gate)");
        model.NextCommand.Execute("area");
        model.AreaLabel.Should().Be("Interstellar");
        model.NextCommand.Execute("area");
        model.AreaLabel.Should().Be("All areas");
        model.SystemLabel.Should().Be("Stanton");
        model.PreviousCommand.Execute("system");
        model.SystemLabel.Should().Be("Interstellar");
        _saved.Should().BeEmpty("only rank and faction are kept");
    }

    // --- Recipe (T13.3b, D123) ---

    private const string L1 = "HUR-L1 Green Glade Station";

    private static readonly Ship HullA = FrozenShips.Load("hull_a");
    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    private static readonly HaulTemplate SeniorBulk = T([], [L1], reward: 325_250, min: 41, max: 432, title: "Senior Rank - Medium Cargo Haul");

    // From L1 back to the board's hub (D121): an empty drop-off pool.
    private static readonly HaulTemplate JuniorBack = T([L1], [], reward: 72_500, title: "Junior Rank - Small Cargo Haul") with
    {
        Size = HaulSize.Small,
        Legs = [new HaulLeg("Gold", 4, 16, CrateSize.Four)],
    };

    [Fact]
    public void The_view_switches_between_matrix_and_recipe_and_the_hub_cycles_and_is_saved()
    {
        var model = Model([SeniorBulk]);
        model.ViewLabel.Should().Be("Matrix");
        model.IsRecipe.Should().BeFalse();

        model.NextCommand.Execute("view");
        model.ViewLabel.Should().Be("Recipe");
        model.IsRecipe.Should().BeTrue();
        model.HubLabel.Should().Be("Hub: Everus Harbor");

        model.NextCommand.Execute("hub");
        model.HubLabel.Should().Be("Hub: Seraphim Station");
        model.PreviousCommand.Execute("hub");
        model.PreviousCommand.Execute("hub");
        model.HubLabel.Should().Be("Hub: Port Tressler");
        _saved.Select(s => s.Hub).Should().Equal("Seraphim Station", "Everus Harbor", "Port Tressler");

        model.PreviousCommand.Execute("view");
        model.IsRecipe.Should().BeFalse();
    }

    [Fact]
    public void The_recipe_lists_its_roles_and_the_way_back_for_the_ship()
    {
        var model = Model([SeniorBulk, JuniorBack]);
        model.ShowShip(HullB);

        model.NextCommand.Execute("view");

        (model.ShowsMatrix, model.ShowsRecipe).Should().Be((false, true));
        model.WithoutPlaceText.Should().BeNull("the matrix's lines say nothing about the recipe");
        model.RecipeLines.Should().Equal(
            "Bulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · 41–432 SCU in 16s · HUR-L1",
            "Back · Junior Rank - Small Cargo Haul · 72,500 aUEC · 4–16 SCU in 4s · accept at HUR-L1");
        model.RecipeSummary.Should().Be("Σ 325,250 aUEC · worst case 432 / 512 SCU in 27 crates · fits MISC Hull B");
        model.RecipeNote.Should().BeNull();
    }

    [Fact]
    public void Another_ship_or_rank_makes_the_recipe_anew()
    {
        var model = Model([SeniorBulk, JuniorBack], RoutesSettings.Default with { Hub = "Everus Harbor" });
        model.ShowShip(HullB);
        model.NextCommand.Execute("view");

        model.ShowShip(HullA);
        model.RecipeLines.Should().BeEmpty("432 SCU don't go into 64");
        model.RecipeNote.Should().Be("Recipe: nothing from this hub fits the ship");

        model.ShowShip(HullB);
        model.Follow(RoutesSettings.Default with { Rank = "junior" });
        model.RecipeLines.Should().BeEmpty("the Senior Medium is above the rank, the Junior is a way back only");
    }

    [Fact]
    public void Pyro_and_no_ship_have_no_recipe_and_say_why()
    {
        var model = Model([SeniorBulk]);
        model.NextCommand.Execute("view");
        model.RecipeNote.Should().Be("Recipe: no ship");

        model.ShowShip(HullB);
        model.NextCommand.Execute("system");

        model.RecipeLines.Should().BeEmpty();
        model.RecipeNote.Should().Be("Recipe: Stanton hubs only");
    }

    [Fact]
    public void The_overlays_ship_is_the_recipes_from_the_start_and_on_every_change()
    {
        var model = Model([SeniorBulk]);
        var overlay = new ViewModels.OverlayViewModel(HullA) { Routes = model };
        model.NextCommand.Execute("view");
        model.RecipeLines.Should().BeEmpty("the Hull A holds 64 SCU");

        overlay.ChangeShip(HullB);

        model.RecipeLines.Should().ContainSingle().Which.Should().StartWith("Bulk · ");
    }

    [Fact]
    public void A_scanned_offer_is_checked_against_the_hubs_recipe_for_the_ship_in_either_view()
    {
        Positions places = new([.. new[] { "Everus Harbor", L1 }.Select(n => new StarPosition(n, n, StarSystem.Stanton, "Manmade", null, 0, 0, 0, []))]);
        var model = Model([SeniorBulk], positions: places);
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Medium, false, 325_250, ["Gold"]);
        var scan = new ObservedScan(key, null, ["Everus Harbor"], [new ObservedDelivery("Green Glade Station", "Gold", 300)], Monday);

        model.FitOf(scan).Should().BeNull("no ship yet");
        model.ShowShip(HullB);
        model.IsRecipe.Should().BeFalse();
        model.FitOf(scan).Should().Be(new RecipeFit.FitsAs(RecipeKind.Bulk));
        model.ShowShip(HullA);
        model.FitOf(scan).Should().Be(new RecipeFit.TooBig(432, CrateSize.Sixteen, HullA.Name));
    }

    // --- Loops (T13.4, D124) ---

    private const string ArcL1 = "ARC-L1 Wide Forest Station";

    // In Gm: Everus ↔ L1 1.3 (short), Everus ↔ Baijini 22.9, Baijini ↔ ARC-L1 3.
    private static readonly Positions Stanton = new(
    [
        At("Everus Harbor", 0), At(L1, 1.3), At("Baijini Point", 22.9), At(ArcL1, 25.9), At("Seraphim Station", -30), At("Port Tressler", 40),
        new StarPosition("Ruin Station", "Ruin Station", StarSystem.Pyro, "Manmade", null, 0, 0, 0, []),
        new StarPosition("Checkmate", "Checkmate", StarSystem.Pyro, "Manmade", null, 5e9, 0, 0, []),
    ]);

    private static readonly HaulTemplate EverusBulk = SeniorBulk with { Uuid = "everus-bulk" };

    private static readonly HaulTemplate BaijiniBulk = T([], [ArcL1], reward: 300_000, min: 41, max: 400, areaKey: "Stanton3") with { Uuid = "baijini-bulk" };

    // A direct of 100 SCU from Everus: with the Everus bulk 532 SCU, with the Baijini one 500.
    private static readonly HaulTemplate EverusDirect = T([], [L1], reward: 90_000, title: "Senior Rank - Direct Small Cargo Haul") with
    {
        Uuid = "everus-direct",
        Size = HaulSize.Small,
        Direct = true,
        Legs = [new HaulLeg("Gold", 1, 100, CrateSize.Four)],
    };

    private static readonly HaulTemplate PyroSmall = T(["Ruin Station"], ["Checkmate"], reward: null, max: 20, contractor: HaulContractor.RedWind,
        system: StarSystem.Pyro, areaKey: "Solar", faction: HaulFaction.Hh) with { Uuid = "pyro", Size = HaulSize.Small };

    private static StarPosition At(string name, double gm) => new(name, name, StarSystem.Stanton, "Manmade", null, gm * 1e9, 0, 0, []);

    private RoutesViewModel Loops(RoutesSettings? settings = null, params HaulTemplate[] templates)
    {
        var model = Model(templates.Length > 0 ? templates : [EverusBulk, BaijiniBulk, PyroSmall], settings, positions: Stanton);
        model.NextCommand.Execute("view");
        model.NextCommand.Execute("view");
        return model;
    }

    // What the App does off the UI thread, here at once.
    private static void Compute(RoutesViewModel model) => model.ShowLoops(model.LoopInputs!, model.LoopInputs!.Run());

    [Fact]
    public void The_view_cycles_matrix_recipe_loops_and_back()
    {
        var model = Model([SeniorBulk]);

        model.NextCommand.Execute("view");
        model.NextCommand.Execute("view");
        (model.ViewLabel, model.IsLoops, model.IsRecipe, model.IsMatrix).Should().Be(("Loops", true, false, false));
        (model.ShowsLoops, model.ShowsRecipe, model.ShowsMatrix).Should().Be((true, false, false));

        model.NextCommand.Execute("view");
        model.ViewLabel.Should().Be("Matrix");
        model.PreviousCommand.Execute("view");
        model.ViewLabel.Should().Be("Loops");
    }

    [Fact]
    public void The_loops_are_asked_for_with_a_ship_and_shown_when_they_come()
    {
        var model = Loops();
        List<LoopInputs> asked = [];
        model.LoopInputsChanged += (_, inputs) => asked.Add(inputs);
        model.LoopsNote.Should().Be("Loops: no ship");

        model.ShowShip(HullB);

        asked.Should().ContainSingle().Which.Should().BeSameAs(model.LoopInputs);
        model.LoopInputs!.Hub.Should().Be("Everus Harbor");
        model.LoopsNote.Should().Be("Loops: working them out …");
        Compute(model);
        model.LoopsNote.Should().BeNull();
        model.LoopRows.Select(r => r.Title).Should().Equal("Everus Harbor ring", "Baijini Point ring", "LEO ring from Everus Harbor");
    }

    [Fact]
    public void A_result_for_inputs_that_changed_since_is_dropped()
    {
        var model = Loops();
        model.ShowShip(HullB);
        var old = model.LoopInputs!;
        var result = old.Run();

        model.ShowShip(HullA);
        model.ShowLoops(old, result);

        model.LoopRows.Should().BeEmpty();
        model.LoopsNote.Should().Be("Loops: working them out …");
    }

    [Fact]
    public void A_row_reads_title_figures_and_structure()
    {
        var model = Loops();
        model.ShowShip(HullB);
        Compute(model);

        var everus = model.LoopRows.Single(r => r.Title == "Everus Harbor ring");
        everus.Figures.Should().Be("325k/loop · ~672k/h · 162k/landing", "325,250 aUEC in 2 landings × 10 min + 27 crates × 20 s = 29 min");
        everus.Details.Should().Be("2 landings · 27 crates · 2 jumps (0 long) · travel – · templates");
        everus.ToolTip.Should().StartWith("Everus Harbor → HUR-L1 → Everus Harbor\nBulk · Senior Rank - Medium Cargo Haul · 325,250 aUEC · 432 SCU");
        (everus.CanOpen, everus.Grey).Should().Be((true, false));
        model.LoopRows.Single(r => r.Title == "Baijini Point ring").Details.Should().Contain("travel 22.9 Gm");
        var leo = model.LoopRows.Single(r => r.Title.StartsWith("LEO", StringComparison.Ordinal));
        (leo.Figures, leo.Details, leo.Grey, leo.CanOpen).Should().Be(((string?)null, "needs scans at Everus Harbor", true, false));
    }

    [Fact]
    public void The_sort_cycles_is_saved_and_the_times_from_the_settings_rank_without_a_new_computation()
    {
        var model = Loops();
        model.ShowShip(HullB);
        Compute(model);
        var asked = 0;
        model.LoopInputsChanged += (_, _) => asked++;

        model.SortLabel.Should().Be("Sort: ~aUEC/h");
        model.LoopRows.Select(r => r.Title).Take(2).Should().Equal("Everus Harbor ring", "Baijini Point ring");
        model.NextCommand.Execute("sort");
        model.SortLabel.Should().Be("Sort: aUEC/loop");
        model.LoopRows.Select(r => r.Title).Take(2).Should().Equal("Everus Harbor ring", "Baijini Point ring");
        _saved.Last().LoopsSort.Should().Be("loop");

        // Baijini's two long jumps weigh little when a jump is short, much when it is long.
        model.TimesText.Should().Be("10 min · 20 s · 4 min");
        model.PreviousCommand.Execute("sort");
        model.Follow(RoutesSettings.Default with { Times = new RoutesTimes(10, 20, 1) });
        model.TimesText.Should().Be("10 min · 20 s · 1 min");
        model.LoopRows.Select(r => r.Title).Take(2).Should().Equal("Everus Harbor ring", "Baijini Point ring");
        asked.Should().Be(0, "times and sort only rank");
    }

    [Fact]
    public void The_system_filter_shows_its_systems_loops()
    {
        var model = Loops();
        model.ShowShip(HullB);
        Compute(model);

        model.LoopRows.Should().OnlyContain(r => r.Loop.System == StarSystem.Stanton);
        model.NextCommand.Execute("system");
        model.LoopRows.Select(r => (r.Title, r.Figures)).Should().Equal(("Ruin Station ring", "– aUEC: no reward in the game data, a scan shows it"));
        model.LoopRows.Single().Details.Should().Contain("travel: Pyro").And.Contain("templates");
        model.NextCommand.Execute("system");
        model.LoopRows.Should().BeEmpty();
        model.LoopsNote.Should().Be("No loops for this system.");
    }

    [Fact]
    public void A_rows_click_shows_its_hubs_recipe_for_the_session_and_the_offer_check_stays_at_routes_hub()
    {
        // K1 of the T13.4 approval: the scan happens at routes.hub, whatever the recipe shows.
        var model = Loops(null, EverusBulk, EverusDirect, BaijiniBulk);
        model.ShowShip(HullB);
        Compute(model);
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Small, true, 90_000, ["Gold"]);
        var scan = new ObservedScan(key, null, ["Everus Harbor"], [new ObservedDelivery("Green Glade Station", "Gold", 50)], Monday);
        var match = TemplateMatch.Find(Observation.Of(scan), [EverusBulk, EverusDirect, BaijiniBulk], Stanton);
        var atBaijini = RecipeFit.Of(match, Recipe.For(Board.For([EverusBulk, EverusDirect, BaijiniBulk], StarSystem.Stanton, "Stanton3", HaulRank.Master, null), HullB, ObservedEdges.Empty), HullB);
        atBaijini.Should().Be(new RecipeFit.FitsAs(RecipeKind.Direct), "Baijini's recipe would take it: this test tells the hubs apart");

        model.OpenLoopCommand.Execute(model.LoopRows.Single(r => r.Title == "Baijini Point ring"));

        (model.IsRecipe, model.HubLabel).Should().Be((true, "Hub: Baijini Point (from Loops)"));
        model.RecipeLines.Should().ContainSingle().Which.Should().Contain("300,000 aUEC");
        _saved.Should().BeEmpty("routes.hub stays");
        model.FitOf(scan).Should().Be(new RecipeFit.TooBig(100, CrateSize.Four, HullB.Name), "432 + 100 SCU at Everus");

        model.NextCommand.Execute("hub");
        model.HubLabel.Should().Be("Hub: Port Tressler", "the hub button goes on from the hub shown and saves");
        _saved.Select(s => s.Hub).Should().Equal("Port Tressler");
    }

    [Fact]
    public void A_row_without_a_recipe_does_nothing_on_a_click()
    {
        var model = Loops();
        model.ShowShip(HullB);
        Compute(model);

        model.OpenLoopCommand.Execute(model.LoopRows.Single(r => r.Title.StartsWith("LEO", StringComparison.Ordinal)));

        model.IsLoops.Should().BeTrue();
        model.HubLabel.Should().Be("Hub: Everus Harbor");
    }

    [Fact]
    public void Rank_hub_and_new_observations_ask_for_new_loops()
    {
        var model = Loops();
        model.ShowShip(HullB);
        List<LoopInputs> asked = [];
        model.LoopInputsChanged += (_, inputs) => asked.Add(inputs);

        model.NextCommand.Execute("rank");
        model.NextCommand.Execute("faction");
        model.NextCommand.Execute("hub");
        model.ShowObserved(ObservedEdges.Empty);
        model.Follow(model.LoopInputs is { } ? RoutesSettings.Default with { Rank = "junior" } : RoutesSettings.Default);

        asked.Select(a => (a.Rank, a.Faction, a.Hub)).Should().Equal(
            (HaulRank.Trainee, (HaulFaction?)null, "Everus Harbor"),
            (HaulRank.Trainee, HaulFaction.Hh, "Everus Harbor"),
            (HaulRank.Trainee, HaulFaction.Hh, "Seraphim Station"),
            (HaulRank.Trainee, HaulFaction.Hh, "Seraphim Station"),
            (HaulRank.Junior, (HaulFaction?)null, "Seraphim Station"));
    }

    [Fact]
    public void Without_hauling_data_the_tab_says_why_and_shows_no_matrix()
    {
        var none = new RoutesViewModel(new HaulingLoad.None("ships-2026.11.1 has no hauling data"), new ShipDataVersion(2026, 11, 1), RoutesSettings.Default, null);
        var unknown = new RoutesViewModel(new HaulingLoad.UnknownFormat("positions.json doesn't match"), Version, RoutesSettings.Default, null);

        none.HasData.Should().BeFalse();
        none.StatusText.Should().Be("No hauling data in ship data 2026.11.1 – update ship data");
        none.Rows.Should().BeEmpty();
        none.EmptyText.Should().BeNull();
        unknown.StatusText.Should().Be("The hauling data in ship data 2026.11.2 can't be used by this app – see cargoplan.log");
    }
}
