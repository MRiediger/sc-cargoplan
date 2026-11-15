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

    // T10.5: every scan's picture for a bug report; off by default, the pictures are CIG material and stay with the player.
    [Theory]
    [InlineData("--keep-scans")]
    [InlineData("--Keep-Scans")]
    public void Keep_scans_keeps_the_scan_pictures(string arg)
    {
        StartOptions.Parse([arg]).Should().Be(new StartOptions(false, null, null, KeepScans: true));
    }

    // D108: who never scans pays nothing; the probe measures with a warm engine whatever the key.
    [Theory]
    [InlineData(true, new string[0], true)]
    [InlineData(false, new string[0], false)]
    [InlineData(false, new[] { "--scan-probe" }, true)]
    public void The_ocr_warms_at_start_only_with_a_scan_key_or_the_probe(bool scanKeySet, string[] args, bool warms)
    {
        StartOptions.Parse(args).WarmsOcrAtStart(scanKeySet).Should().Be(warms);
    }

    // T11.1 (D109, E10): a local feed instead of GitHub, for gates without a real release.
    [Theory]
    [InlineData("--updates-source", @"D:\p11-run\feed")]
    [InlineData("--Updates-Source", @"D:\p11-run\feed")]
    public void Updates_source_takes_the_next_argument_as_feed_folder(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null, UpdatesSource: @"D:\p11-run\feed"));
    }

    [Theory]
    [InlineData("--updates-source")]
    [InlineData("--updates-source", "--locked")]
    [InlineData("--updates-source", "feed")]
    public void Updates_source_without_a_full_path_is_a_problem_not_a_stop(params string[] args)
    {
        // Unlike --data-dir it protects no file: the overlay starts and checks GitHub as without the switch.
        var options = StartOptions.Parse(args);

        options.UpdatesSource.Should().BeNull();
        options.DataDirRefused.Should().BeFalse();
        options.Problem.Should().Be("--updates-source needs the full path of a folder.");
        options.Locked.Should().Be(args.Contains("--locked"));
    }

    // T11.3 (R9): a developer's switch, so the gate can update rc.1 → rc.2 over GitHub.
    [Theory]
    [InlineData("--updates-prerelease")]
    [InlineData("--Updates-Prerelease")]
    public void Updates_prerelease_lets_the_check_see_pre_releases(params string[] args)
    {
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null, UpdatesPrerelease: true));
    }

    [Fact]
    public void Updates_prerelease_with_a_local_feed_is_a_problem()
    {
        // A local feed has no pre-release flag to filter by; the feed is used as it is, the switch is dropped.
        var options = StartOptions.Parse(["--updates-source", @"D:\run\releases", "--updates-prerelease"]);

        options.UpdatesSource.Should().Be(@"D:\run\releases");
        options.UpdatesPrerelease.Should().BeFalse();
        options.Problem.Should().Be("--updates-prerelease only goes with GitHub, not with --updates-source.");
    }

    [Theory]
    [InlineData("--veloapp-updated", "0.9.1")]
    [InlineData("--veloapp-obsolete", "0.9.0")]
    [InlineData("--veloapp-firstrun")]
    public void Velopack_hook_arguments_are_ignored(params string[] args)
    {
        // Velopack ends a hook run inside Run() (S11.0 G4); should one get through, it must not change the start.
        StartOptions.Parse(args).Should().Be(new StartOptions(false, null, null));
    }

    [Fact]
    public void The_restart_after_an_update_gets_the_same_arguments()
    {
        // U5: the restarted overlay starts as this one did (lock, data folder, feed, scans).
        string[] args = ["--locked", "--data-dir", @"D:\run\data", "--updates-source", @"D:\run\releases", "--keep-scans"];

        StartOptions.RestartArguments(args).Should().Equal(args);
    }

    [Fact]
    public void The_restart_after_an_update_keeps_seeing_pre_releases()
    {
        // Gate 2 (T11.3): rc.2, started by Update.exe, must check as rc.1 did.
        string[] args = ["--updates-prerelease", "--data-dir", @"D:\run\data"];

        StartOptions.RestartArguments(args).Should().Equal(args);
    }

    [Theory]
    [InlineData("--plan", @"D:\plans\route.json")]
    [InlineData("--Plan", @"D:\plans\route.json")]
    public void The_restart_leaves_out_the_start_plan(params string[] plan)
    {
        // Freigabe: the file would replace plan.json, and with it every tick of the run.
        string[] args = ["--locked", .. plan, "--data-dir", @"D:\run\data"];

        StartOptions.RestartArguments(args).Should().Equal("--locked", "--data-dir", @"D:\run\data");
    }

    [Fact]
    public void A_plan_switch_without_a_path_takes_nothing_else_with_it()
    {
        StartOptions.RestartArguments(["--plan", "--locked"]).Should().Equal("--locked");
    }
}
