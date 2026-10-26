using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The shipped cargo module (K5.3, D59): the Aurora Mk II's TS module, counted in game by Martin (26 Oct 2026). A
/// platform of 3 × 2 × 1 cells at the stern that takes crates up to 2 SCU, 8 SCU with the ship's own 2.
/// </summary>
public sealed class ShipModuleTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship Base = Catalog.Get("aurora_mk_ii");
    private static readonly Ship WithModule = Catalog.Get("aurora_mk_ii_ts_module");

    // 6 SCU at most 2 per crate: three 2s, which no 1 × 1 × 1 rack of the base takes.
    private static readonly MissionStop Riker = new("Riker", 1, [new MissionItem("Titanium", 6, CrateSize.Two, null)]);

    private static SavedPlan Planned(Ship ship) => LoadPlanner.Plan(ship, [new Mission(1, [Riker])], ["Riker"]);

    [Fact]
    public void The_ts_module_is_a_catalog_entry_of_its_own_with_8_SCU()
    {
        WithModule.Name.Should().Be("RSI Aurora Mk II · TS module");
        WithModule.TotalScu.Should().Be(8);
        WithModule.Racks.Should().HaveCount(3);
        var platform = WithModule.Racks[^1];
        (platform.Grid, platform.MaxCrateSize).Should().Be((new Dimensions(3, 2, 1), (CrateSize?)CrateSize.Two));
        WithModule.FleetyardsSlug.Should().Be(Base.FleetyardsSlug);
        Catalog.Problems.Should().NotContain(p => p.ShipId.Contains("ts_module", StringComparison.Ordinal));
    }

    [Fact]
    public void The_base_ship_stays_as_it_was()
    {
        (Base.Name, Base.TotalScu, Base.Racks.Count).Should().Be(("RSI Aurora Mk II", 2, 2));
    }

    // Martin at the holo gate (K5.3b): the racks sat a cell too high in the hull; the hologram goes up one cell, for both.
    [Fact]
    public void The_aurora_mk_ii_lifts_its_hologram_one_cell_with_and_without_the_module()
    {
        Base.HoloOffset.Should().Be(new ShipPoint(0, 0, 1));
        WithModule.HoloOffset.Should().Be(new ShipPoint(0, 0, 1));
    }

    [Fact]
    public void Without_the_module_6_SCU_in_2s_is_refused()
    {
        var act = () => Planned(Base);

        act.Should().Throw<PlanException>();
    }

    [Fact]
    public void With_the_module_6_SCU_in_2s_go_onto_the_platform()
    {
        var platform = WithModule.Racks[^1].Id;

        var saved = Planned(WithModule);

        saved.Plan.Crates.Should().HaveCount(3).And.OnlyContain(c => c.CrateSize == CrateSize.Two && c.RackId == platform);
        LoadPlanner.Validate(saved.Plan, WithModule).Should().BeEmpty();
    }
}
