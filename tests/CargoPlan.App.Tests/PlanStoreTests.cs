using CargoPlan.App.Plans;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>Works on a fresh temp folder per test; xUnit creates a new instance for every test.</summary>
public sealed class PlanStoreTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private string PlanPath => Path.Combine(_folder, "plan.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(PlanPath, content);
    }

    private static SavedPlan RikerDelivered()
    {
        var planned = TestPlans.SplitAndShared();
        return new SavedPlan(planned.Missions, LoadPlanner.SetDelivered(planned.Plan, HullA, "Riker", true), planned.CrateCap);
    }

    [Fact]
    public void Save_then_Load_keeps_the_last_scan_number()
    {
        // D102: kept beside the plan, outside the undo steps; a plan without scans has 0.
        PlanStore.Save(PlanPath, RikerDelivered(), lastScanNo: 4);

        PlanStore.Load(PlanPath, HullA).LastScanNo.Should().Be(4);
    }

    [Fact]
    public void Save_then_Load_keeps_the_plan_with_its_ticks()
    {
        var saved = RikerDelivered();

        PlanStore.Save(PlanPath, saved);
        var (loaded, problem, _) = PlanStore.Load(PlanPath, HullA);

        problem.Should().BeNull();
        loaded!.Plan.Placements.Should().Equal(saved.Plan.Placements);
        loaded.Plan.Warnings.Should().Equal(saved.Plan.Warnings);
        loaded.Missions.Should().BeEquivalentTo(saved.Missions, o => o.WithStrictOrdering());
        loaded.Plan.PlacementsForStop("Riker").Should().OnlyContain(p => p.Delivered);
    }

    /// <summary>The fill order chosen in the editor survives a restart (T4.5).</summary>
    [Fact]
    public void Save_then_Load_keeps_the_fill_order()
    {
        var planned = TestPlans.SplitAndShared();
        var saved = SavedPlan.FromStops(planned.Stops, LoadPlanner.Plan(HullA, planned.Stops.ToStops(), FillOrder.SternFirst), CrateCap.Of(HullA));

        PlanStore.Save(PlanPath, saved);
        var (loaded, problem, _) = PlanStore.Load(PlanPath, HullA);

        problem.Should().BeNull();
        loaded!.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        loaded.Plan.Crates.Should().Equal(saved.Plan.Crates);
        File.ReadAllText(PlanPath).Should().Contain("\"fillOrder\": \"sternFirst\"");
    }

    [Fact]
    public void Missing_file_gives_no_plan_without_a_problem()
    {
        var (loaded, problem, _) = PlanStore.Load(PlanPath, HullA);

        loaded.Should().BeNull();
        problem.Should().BeNull();
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("null")]
    [InlineData("""{ "schemaVersion": 2 }""")]
    public void Broken_file_gives_no_plan_with_a_problem_and_stays_untouched(string content)
    {
        WriteFile(content);

        var (loaded, problem, _) = PlanStore.Load(PlanPath, HullA);

        loaded.Should().BeNull();
        problem.Should().StartWith("Plan not loaded").And.Contain(PlanPath);
        File.ReadAllText(PlanPath).Should().Be(content);
    }

    [Fact]
    public void Broken_file_writes_its_notice_to_the_log()
    {
        WriteFile("this is not json");

        var (_, problem, _) = PlanStore.Load(PlanPath, HullA);

        // The path holds this test's own folder, so parallel tests writing the same log don't matter (K10, F9).
        TestLog.Text().Should().Contain($" Plan: {problem} [PlanFormatException]");
    }

    [Fact]
    public void Plan_for_another_ship_loads_with_its_ship_and_stays_untouched()
    {
        // The overlay plans it anew for its ship (D59); the file changes only when the replanned plan is saved.
        PlanStore.Save(PlanPath, TestPlans.SplitAndShared());
        var content = File.ReadAllText(PlanPath).Replace("\"shipId\": \"hull_a\"", "\"shipId\": \"cutlass_black\"");
        File.WriteAllText(PlanPath, content);

        var (loaded, problem, _) = PlanStore.Load(PlanPath, HullA);

        problem.Should().BeNull();
        loaded!.Plan.ShipId.Should().Be("cutlass_black");
        File.ReadAllText(PlanPath).Should().Be(content);
    }

    // --- An unreadable plan.json is kept aside, never overwritten (B13, D106) ---

    private static readonly DateTime Now = new(2026, 11, 11, 14, 23, 1);

    private string KeptPath(string name) => Path.Combine(_folder, name);

    [Fact]
    public void Broken_file_is_unreadable()
    {
        WriteFile("{ \"schemaVersion\": 10, \"shipId\": \"hu");

        PlanStore.Load(PlanPath, HullA).Unreadable.Should().BeTrue();
    }

    [Fact]
    public void Missing_file_good_file_and_another_ships_plan_are_not_unreadable()
    {
        PlanStore.Load(PlanPath, HullA).Unreadable.Should().BeFalse("no file is nothing to keep");

        PlanStore.Save(PlanPath, TestPlans.SplitAndShared());
        PlanStore.Load(PlanPath, HullA).Unreadable.Should().BeFalse();

        // D59: planned anew for this ship, never a problem.
        File.WriteAllText(PlanPath, File.ReadAllText(PlanPath).Replace("\"shipId\": \"hull_a\"", "\"shipId\": \"cutlass_black\""));
        PlanStore.Load(PlanPath, HullA).Unreadable.Should().BeFalse();
    }

    [Fact]
    public void KeepUnreadable_moves_the_file_aside_byte_for_byte()
    {
        WriteFile("{ \"schemaVersion\": 10, \"shipId\": \"hu");
        var bytes = File.ReadAllBytes(PlanPath);

        var keptAs = PlanStore.KeepUnreadable(PlanPath, Now);

        keptAs.Should().Be("plan.json.unreadable-20261111-142301");
        File.Exists(PlanPath).Should().BeFalse();
        File.ReadAllBytes(KeptPath(keptAs!)).Should().Equal(bytes);
    }

    [Fact]
    public void KeepUnreadable_twice_in_the_same_second_keeps_both()
    {
        WriteFile("first");
        PlanStore.KeepUnreadable(PlanPath, Now);
        WriteFile("second");

        var second = PlanStore.KeepUnreadable(PlanPath, Now);

        second.Should().Be("plan.json.unreadable-20261111-142301-2");
        File.ReadAllText(KeptPath("plan.json.unreadable-20261111-142301")).Should().Be("first");
        File.ReadAllText(KeptPath(second!)).Should().Be("second");
    }

    [Fact]
    public void KeepUnreadable_without_a_file_keeps_nothing()
    {
        PlanStore.KeepUnreadable(PlanPath, Now).Should().BeNull();
    }

    // --- The start plan: --plan wins, plan.json's state is reported either way (B13) ---

    [Fact]
    public void LoadStart_with_a_plan_file_reads_it_and_reports_an_unreadable_plan_json()
    {
        WriteFile("{ \"schemaVersion\": 10, \"shipId\": \"hu");
        var other = Path.Combine(_folder, "start.json");
        PlanStore.Save(other, TestPlans.SplitAndShared(), lastScanNo: 3);

        var loaded = PlanStore.LoadStart(PlanPath, other, HullA);

        loaded.Plan.Should().NotBeNull();
        loaded.Problem.Should().BeNull();
        loaded.LastScanNo.Should().Be(3);
        loaded.Unreadable.Should().BeTrue("the plan file becomes plan.json at the first save");
    }

    [Fact]
    public void LoadStart_with_a_missing_plan_file_names_it()
    {
        var missing = Path.Combine(_folder, "missing.json");

        var loaded = PlanStore.LoadStart(PlanPath, missing, HullA);

        loaded.Plan.Should().BeNull();
        loaded.Problem.Should().Be($"Plan file {missing} not found.");
        loaded.Unreadable.Should().BeFalse();
    }

    [Fact]
    public void LoadStart_without_a_plan_file_loads_plan_json()
    {
        WriteFile("{ \"schemaVersion\": 10, \"shipId\": \"hu");

        var loaded = PlanStore.LoadStart(PlanPath, null, HullA);

        loaded.Plan.Should().BeNull();
        loaded.Problem.Should().StartWith("Plan not loaded");
        loaded.Unreadable.Should().BeTrue();
    }

    [Fact]
    public void PeekShipId_reads_the_plans_ship_and_null_without_a_file()
    {
        PlanStore.PeekShipId(PlanPath).Should().BeNull();

        PlanStore.Save(PlanPath, TestPlans.SplitAndShared());

        PlanStore.PeekShipId(PlanPath).Should().Be("hull_a");
    }

    [Fact]
    public void Save_creates_the_missing_folder_and_leaves_no_temp_file_behind()
    {
        PlanStore.Save(PlanPath, TestPlans.SplitAndShared());
        PlanStore.Save(PlanPath, RikerDelivered());

        Directory.GetFiles(_folder).Should().ContainSingle().Which.Should().Be(PlanPath);
        File.ReadAllText(PlanPath).Should().Contain("\"delivered\": true");
    }

    [Fact]
    public void DefaultPath_is_next_to_the_settings_but_its_own_file()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        PlanStore.DefaultPath.Should().Be(Path.Combine(localAppData, "SC-CargoPlan", "plan.json"));
    }
}
