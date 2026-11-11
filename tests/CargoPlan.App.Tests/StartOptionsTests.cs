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

    [Theory]
    [InlineData("--theme-sample")]
    [InlineData("--Theme-Sample")]
    public void Theme_sample_switch_ignores_case(string arg)
    {
        StartOptions.Parse([arg]).Should().Be(new StartOptions(false, null, null, ThemeSample: true));
    }

    [Fact]
    public void Theme_sample_is_off_by_default()
    {
        StartOptions.Parse(["--locked"]).ThemeSample.Should().BeFalse();
    }

    [Fact]
    public void Unknown_arguments_are_ignored()
    {
        // Windows may pass extra arguments (e.g. a shortcut); they must not keep the overlay from starting.
        StartOptions.Parse(["--verbose", "x"]).Should().Be(new StartOptions(false, null, null));
    }

    [Theory]
    [InlineData("--measure-frames", "hull_c")]
    [InlineData("--Measure-Frames", "hull_c")]
    public void Measure_frames_switch_takes_the_next_argument_as_ship(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null, MeasureFrames: "hull_c"));
    }

    [Theory]
    [InlineData("--measure-frames")]
    [InlineData("--measure-frames", "--locked")]
    public void Measure_frames_switch_without_a_ship_is_a_problem(params string[] args)
    {
        var options = StartOptions.Parse(args);

        options.MeasureFrames.Should().BeNull();
        options.Problem.Should().Contain("--measure-frames");
    }

    [Fact]
    public void Measure_frames_is_off_by_default()
    {
        StartOptions.Parse(["--locked"]).MeasureFrames.Should().BeNull();
    }

    [Theory]
    [InlineData("--measure-frames", "hull_c", "--over-game")]
    [InlineData("--Over-Game", "--measure-frames", "hull_c")]
    public void Over_game_lays_the_measuring_window_over_the_game(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null, MeasureFrames: "hull_c", OverGame: true));
    }

    [Fact]
    public void Over_game_without_a_measurement_is_a_problem()
    {
        // The overlay lies over the game anyway; the switch only changes the measuring window (T8.7).
        var options = StartOptions.Parse(["--over-game"]);

        options.OverGame.Should().BeFalse();
        options.Problem.Should().Contain("--over-game");
    }

    [Fact]
    public void Over_game_is_off_by_default()
    {
        StartOptions.Parse(["--measure-frames", "hull_c"]).OverGame.Should().BeFalse();
    }

    // T10.1b: F8 = one scan, F9 = a series; the number is the series' length.
    [Theory]
    [InlineData(100, "--scan-probe")]
    [InlineData(100, "--Scan-Probe", "--locked")]
    [InlineData(20, "--scan-probe", "20")]
    public void Scan_probe_arms_the_probe_keys_with_the_series_length(int series, params string[] args)
    {
        var options = StartOptions.Parse(args);

        options.ScanProbe.Should().Be(series);
        options.Problem.Should().BeNull();
    }

    [Theory]
    [InlineData("--scan-probe", "0")]
    [InlineData("--scan-probe", "-5")]
    [InlineData("--scan-probe", "many")]
    public void Scan_probe_with_a_series_that_is_no_positive_number_is_a_problem_and_keeps_the_default(params string[] args)
    {
        var options = StartOptions.Parse(args);

        options.ScanProbe.Should().Be(StartOptions.ScanProbeSeries);
        options.Problem.Should().Contain("--scan-probe");
    }

    [Fact]
    public void Scan_probe_is_off_by_default()
    {
        var options = StartOptions.Parse(["--locked"]);

        options.ScanProbe.Should().BeNull();
        options.ScanProbeImages.Should().BeFalse();
    }

    [Fact]
    public void Scan_probe_images_keep_every_region_as_a_picture()
    {
        StartOptions.Parse(["--scan-probe", "--scan-probe-images"]).ScanProbeImages.Should().BeTrue();
    }

    // K3: the frame measurement under scans has its own switch; --scan-probe always means the probe keys.
    [Theory]
    [InlineData("--measure-frames", "hull_c", "--over-game", "--scan-load")]
    [InlineData("--Scan-Load", "--measure-frames", "hull_c", "--over-game")]
    public void Scan_load_scans_during_a_frame_measurement(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null, MeasureFrames: "hull_c", OverGame: true, ScanLoad: true));
    }

    [Fact]
    public void Scan_load_without_a_measurement_is_a_problem()
    {
        var options = StartOptions.Parse(["--scan-load"]);

        options.ScanLoad.Should().BeFalse();
        options.Problem.Should().Contain("--scan-load");
    }

    [Fact]
    public void Scan_probe_next_to_a_measurement_does_not_load_it()
    {
        var options = StartOptions.Parse(["--measure-frames", "hull_c", "--scan-probe"]);

        options.ScanLoad.Should().BeFalse();
        options.ScanProbe.Should().Be(StartOptions.ScanProbeSeries);
    }

    [Fact]
    public void Scan_probe_images_without_the_probe_are_a_problem()
    {
        var options = StartOptions.Parse(["--scan-probe-images"]);

        options.ScanProbeImages.Should().BeFalse();
        options.Problem.Should().Contain("--scan-probe-images");
    }

    // T10.4a, B1: a test run with its own files, never the player's profile.
    [Theory]
    [InlineData("--data-dir", @"D:\cargoplan-gate")]
    [InlineData("--Data-Dir", @"D:\cargoplan-gate")]
    public void Data_dir_takes_the_next_argument_as_folder(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null, DataDir: @"D:\cargoplan-gate"));
    }

    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--data-dir", "--locked")]
    [InlineData("--data-dir", "gate")]
    [InlineData("--data-dir", @"..\gate")]
    public void Data_dir_without_a_full_path_refuses_the_start(params string[] args)
    {
        // Falling back to the profile is what the switch is there to prevent (T10.1b): the app ends instead.
        var options = StartOptions.Parse(args);

        options.DataDir.Should().BeNull();
        options.DataDirRefused.Should().BeTrue();
        options.Problem.Should().Be("--data-dir needs the full path of a folder.");
    }

    [Fact]
    public void Without_data_dir_nothing_is_refused()
    {
        StartOptions.Parse(["--locked"]).DataDirRefused.Should().BeFalse();
    }
}
