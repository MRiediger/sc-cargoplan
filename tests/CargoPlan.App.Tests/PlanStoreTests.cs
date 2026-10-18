using CargoPlan.App.Plans;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>Works on a fresh temp folder per test; xUnit creates a new instance for every test.</summary>
public sealed class PlanStoreTests : IDisposable
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

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
        return new SavedPlan(planned.Missions, LoadPlanner.SetDelivered(planned.Plan, HullA, "Riker", true));
    }

    [Fact]
    public void Save_then_Load_keeps_the_plan_with_its_ticks()
    {
        var saved = RikerDelivered();

        PlanStore.Save(PlanPath, saved);
        var (loaded, problem) = PlanStore.Load(PlanPath, HullA);

        problem.Should().BeNull();
        loaded!.Plan.Placements.Should().Equal(saved.Plan.Placements);
        loaded.Plan.Warnings.Should().Equal(saved.Plan.Warnings);
        loaded.Missions.Should().BeEquivalentTo(saved.Missions, o => o.WithStrictOrdering());
        loaded.Plan.PlacementsForStop("Riker").Should().OnlyContain(p => p.Delivered);
    }

    [Fact]
    public void Missing_file_gives_no_plan_without_a_problem()
    {
        var (loaded, problem) = PlanStore.Load(PlanPath, HullA);

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

        var (loaded, problem) = PlanStore.Load(PlanPath, HullA);

        loaded.Should().BeNull();
        problem.Should().StartWith("Plan not loaded").And.Contain(PlanPath);
        File.ReadAllText(PlanPath).Should().Be(content);
    }

    [Fact]
    public void Plan_for_another_ship_gives_a_problem_and_stays_untouched()
    {
        PlanStore.Save(PlanPath, TestPlans.SplitAndShared());
        var content = File.ReadAllText(PlanPath).Replace("\"shipId\": \"hull_a\"", "\"shipId\": \"cutlass_black\"");
        File.WriteAllText(PlanPath, content);

        var (loaded, problem) = PlanStore.Load(PlanPath, HullA);

        loaded.Should().BeNull();
        problem.Should().Contain("'cutlass_black'").And.Contain("'hull_a'");
        File.ReadAllText(PlanPath).Should().Be(content);
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
