using CargoPlan.App.Routing;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Routing;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The station bar above the block "Plan" (T13.6 6b, D126): where the route stands, ‹ › by hand and on its own with the
/// ticks, the ways back of the stop with fits / too big, "Finish plan" at the hub with the run kept and a notice.
/// </summary>
public sealed class RouteBarTests
{
    private const string Everus = "Everus Harbor";
    private const string Riker = "Riker";
    private const string Orison = "Orison";

    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static readonly DateTime Monday = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Positions Places = new(
    [
        At(Everus, 0, 0), At(Riker, 0.5, 0), At(Orison, 30, 0),
    ]);

    private readonly List<Route?> _saved = [];

    private readonly List<RouteRun> _runs = [];

    private DateTime _now = Monday;

    private static Placeholder Out(int id, string to, long reward) =>
        new(id, RecipeKind.Small, $"t-{id}", "Junior Rank - Small Cargo Haul", reward, [new HaulLeg("Stims", 1, 8, CrateSize.Four)], [to]);

    private static Placeholder Back(int id, string acceptAt, int scu, CrateSize crate, IReadOnlyList<string>? collect = null) =>
        new(id, RecipeKind.Return, $"b-{id}", "Junior Rank - Small Cargo Haul", 60_000, [new HaulLeg("Waste", 1, scu, crate)], [Everus], acceptAt, collect ?? []);

    private static Route Ring(params Placeholder[] placeholders) =>
        new("Everus ring", Monday, Everus, LoopKind.LocalRing, StarSystem.Stanton, [Riker, Orison], placeholders, []);

    private static SavedPlan Cargo()
    {
        var plan = new SavedPlan([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));
        plan = LoadPlanner.PlaceInto(plan, HullA, [new MissionStop(Riker, 1, [new MissionItem("Stims", 8, CrateSize.Four)])],
            new ContractSource(new ScanIdentity(1, "KEY-1", false), Reward: 50_250));
        return LoadPlanner.PlaceInto(plan, HullA, [new MissionStop(Orison, 1, [new MissionItem("Gold", 8, CrateSize.Four)])],
            new ContractSource(new ScanIdentity(2, "KEY-2", false), Reward: 40_000));
    }

    private readonly List<bool> _block = [];

    private ActiveRouteViewModel Model(Route? route, bool blockOpen = true) =>
        new(route, _saved.Add, new HaulingLoad.Loaded(new HaulingData([], Places), []), _runs.Add, () => _now, blockOpen, _block.Add);

    [Fact]
    public void The_block_is_open_from_Start_plan_folds_at_the_first_departure_and_a_click_on_its_head_toggles_it_for_the_next_start()
    {
        // Gate 6b (Martin, 25 Nov): Start plan → open; › → closed; click → open; restart → stays.
        var model = Model(null, blockOpen: false);
        model.Show(Cargo(), HullA);

        model.Start(Ring(Out(1, Riker, 50_000))).Should().BeTrue();
        (model.IsBlockOpen, model.Header).Should().Be((true, "▾ Plan: Everus ring · 0/1 filled · Σ 50,000 out · +0 back"));

        model.NextStationCommand.Execute(null);
        (model.IsBlockOpen, model.Header![0]).Should().Be((false, '▸'));
        model.NextStationCommand.Execute(null);
        model.IsBlockOpen.Should().BeFalse("only the first departure folds it");

        model.ToggleBlockCommand.Execute(null);
        model.IsBlockOpen.Should().BeTrue();
        _block.Should().Equal(true, false, true);

        Model(_saved.Last(), blockOpen: _block.Last()).IsBlockOpen.Should().BeTrue("a restart keeps it");
        model.HeaderToolTip.Should().Be(ActiveRouteViewModel.BlockToolTip);
    }

    [Fact]
    public void At_the_hub_at_the_start_the_bar_lists_what_to_accept_there_so_the_block_isnt_needed()
    {
        // Gate 6b (3): every outbound role as an "accept:" line with its places; filled ones with their scan.
        var route = Ring(Out(1, Riker, 50_000) with { FilledBy = "KEY-1" }, Out(2, Orison, 40_000), Back(3, Riker, 8, CrateSize.Four));
        var model = Model(route);
        model.Show(Cargo(), HullA);

        model.StationRows.Select(r => (r.Text, r.Filled)).Should().Equal(
            ("✓ #1 · accept: Small · Junior Rank - Small Cargo Haul · 50,000 aUEC · Riker", true),
            ("accept: Small · Junior Rank - Small Cargo Haul · 40,000 aUEC · Orison", false));
    }

    [Fact]
    public void A_contract_taken_on_the_way_waits_for_its_station_and_names_its_place_there()
    {
        // T13.7b (D125 addendum): the LEO ring listed Baijini's contracts at the start.
        var route = Ring(Out(1, Riker, 50_000), Out(2, Orison, 40_000) with { AcceptAt = Riker });
        var model = Model(route);
        model.Show(Cargo(), HullA);

        model.StationRows.Select(r => r.Text).Should().Equal("accept: Small · Junior Rank - Small Cargo Haul · 50,000 aUEC · Riker");

        model.NextStationCommand.Execute(null);
        model.StationRows.Should().ContainSingle().Which.Text.Should().StartWith("accept: Junior Rank - Small Cargo Haul · 40,000 aUEC · Orison");
        model.Rows.Last().Text.Should().Be("Small · Junior Rank - Small Cargo Haul · 40,000 aUEC · Orison");
        model.Rows.Last().Accept.Should().Be(" · accept at Riker");
    }

    [Fact]
    public void The_bar_names_the_stop_what_it_unloads_and_the_slots_free_and_steps_by_hand()
    {
        var model = Model(Ring());
        model.Show(Cargo(), HullA);

        (model.StationText, model.PreviousStationCommand.CanExecute(null), model.IsAtEnd).Should().Be(("Start · Everus Harbor · 2 contracts · free 8 slots", false, false));
        _now = Monday.AddMinutes(12);
        model.NextStationCommand.Execute(null);
        model.StationText.Should().Be("Station 1/3 · Riker · unload 1 · free 9 slots");
        _saved.Last()!.Should().Match<Route>(r => r.At == Riker && r.DepartedUtc == Monday.AddMinutes(12));
        model.NextStationCommand.Execute(null);
        model.StationText.Should().Be("Station 2/3 · Orison · unload 1 · free 10 slots");
        model.NextStationCommand.Execute(null);
        (model.StationText, model.IsAtEnd, model.NextStationCommand.CanExecute(null)).Should().Be(("Station 3/3 · Everus Harbor · unload 0", true, false));
        model.StationToolTip.Should().Contain("Everus Harbor → Riker 0.5 Gm").And.Contain("Riker → Orison 29.5 Gm (long)");
    }

    [Fact]
    public void Ticking_the_last_line_of_the_bars_station_moves_it_on()
    {
        var model = Model(Ring() with { At = Riker, DepartedUtc = Monday });
        var plan = Cargo();
        model.Show(plan, HullA);

        model.Show(new SavedPlan(plan.Missions, LoadPlanner.SetDelivered(plan.Plan, HullA, Riker, 1, true), plan.CrateCap), HullA);

        model.StationText.Should().StartWith("Station 2/3 · Orison");
        _saved.Last()!.At.Should().Be(Orison);
    }

    [Fact]
    public void The_ways_back_of_the_station_say_fits_or_too_big_and_the_collecting_ones_where_they_were_accepted()
    {
        var route = Ring(Back(3, Riker, 8, CrateSize.Four, collect: [Orison]), Back(4, Riker, 64, CrateSize.Sixteen)) with { At = Riker, DepartedUtc = Monday };
        var model = Model(route);
        model.Show(Cargo(), HullA);

        model.StationRows.Select(r => (r.Text, r.Accept, r.Collect)).Should().Equal(
            ("accept: Junior Rank - Small Cargo Haul · 60,000 aUEC · fits", null, " · collect at Orison"),
            ("accept: Junior Rank - Small Cargo Haul · 60,000 aUEC", " · too big (64 SCU in 16s)", null));
        model.NextStationCommand.Execute(null);
        model.StationRows.Select(r => r.Text).Should().Equal("collect: Junior Rank - Small Cargo Haul (accepted at Riker)");
    }

    [Fact]
    public void Finish_plan_at_the_hub_keeps_the_run_ends_the_route_and_says_what_the_round_paid_in_what_time()
    {
        var overlay = new OverlayViewModel(HullA) { ActiveRoute = Model(Ring(Out(1, Riker, 50_000) with { FilledBy = "KEY-1" }) with { At = Everus, DepartedUtc = Monday.AddMinutes(10) }) };
        overlay.ShowPlan(Cargo());
        _now = Monday.AddMinutes(90);

        overlay.ActiveRoute!.FinishPlanCommand.Execute(null);

        _runs.Should().ContainSingle().Which.Should().Match<RouteRun>(r => r.Reward == 50_250 && r.Time == TimeSpan.FromMinutes(80) && r.Landings == 3
            && r.ShipId == HullA.Id && r.Stations!.SequenceEqual(new[] { Riker, Orison }));
        (overlay.ActiveRoute.HasRoute, _saved.Last()).Should().Be((false, null));
        overlay.Notices.Should().Contain("Plan finished: Everus ring · 50,250 aUEC · 1 h 20 min");
    }

    [Fact]
    public void A_stop_the_bar_lacks_shows_the_start_says_so_once_in_the_log_and_waits_for_the_next_move_to_save()
    {
        // B18 (K4): a place the plan dropped or a station a later data release renamed; the file isn't rewritten unasked.
        var model = Model(Ring() with { At = "Lorville B18", DepartedUtc = Monday });

        model.Show(Cargo(), HullA);
        model.Show(Cargo(), HullA);

        model.StationText.Should().StartWith("Start · Everus Harbor");
        _saved.Should().BeEmpty();
        System.Text.RegularExpressions.Regex.Count(TestLog.Text(), "route position Lorville B18 is no stop of the bar – bar shown at the start").Should().Be(1);
        model.NextStationCommand.Execute(null);
        _saved.Last()!.At.Should().Be(Riker);
    }

    [Fact]
    public void Finish_plan_is_only_at_the_hub_at_the_end()
    {
        var model = Model(Ring() with { At = Orison });
        model.Show(Cargo(), HullA);

        model.FinishPlanCommand.CanExecute(null).Should().BeFalse();
        model.NextStationCommand.Execute(null);
        model.FinishPlanCommand.CanExecute(null).Should().BeTrue();
    }

    private static StarPosition At(string name, double xGm, double yGm) => new(name, name, StarSystem.Stanton, "Manmade", null, xGm * 1e9, yGm * 1e9, 0, []);
}
