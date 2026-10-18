namespace CargoPlan.App.Tests;

public class StartOptionsTests
{
    [Fact]
    public void No_arguments_start_unlocked_with_the_saved_plan()
    {
        StartOptions.Parse([]).Should().Be(new StartOptions(Locked: false, PlanPath: null, Problem: null));
    }

    [Theory]
    [InlineData("--locked")]
    [InlineData("--LOCKED")]
    public void Locked_switch_ignores_case(string arg)
    {
        StartOptions.Parse([arg]).Locked.Should().BeTrue();
    }

    [Fact]
    public void Plan_switch_takes_the_next_argument_as_path()
    {
        StartOptions.Parse(["--plan", @"C:\plans\run.json"]).Should().Be(new StartOptions(false, @"C:\plans\run.json", null));
    }

    [Theory]
    [InlineData("--locked", "--plan", "run.json")]
    [InlineData("--Plan", "run.json", "--locked")]
    public void Switches_work_in_any_order(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(true, "run.json", null));
    }

    [Theory]
    [InlineData("--plan")]
    [InlineData("--plan", "--locked")]
    public void Plan_switch_without_a_path_is_a_problem(params string[] args)
    {
        var options = StartOptions.Parse(args);

        options.PlanPath.Should().BeNull();
        options.Problem.Should().Contain("--plan");
        options.Locked.Should().Be(args.Contains("--locked"));
    }

    [Fact]
    public void Unknown_arguments_are_ignored()
    {
        // Windows may pass extra arguments (e.g. a shortcut); they must not keep the overlay from starting.
        StartOptions.Parse(["--verbose", "x"]).Should().Be(new StartOptions(false, null, null));
    }
}
