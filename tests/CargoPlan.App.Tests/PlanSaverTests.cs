using System.IO;
using System.Windows;
using CargoPlan.App.Plans;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// An unreadable plan.json is kept aside by the first save of the run, never overwritten (B13, D106). The saver is the one
/// writer, so every way of changing the plan is covered: a scan, the editor, a tick, --plan. Fresh temp folder per test.
/// </summary>
public sealed class PlanSaverTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    /// <summary>A plan.json cut off mid-write.</summary>
    private const string Broken = "{ \"schemaVersion\": 10, \"shipId\": \"hu";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private string PlanPath => Path.Combine(_folder, "plan.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private void WriteBroken()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(PlanPath, Broken);
    }

    private string[] Kept() => Directory.Exists(_folder) ? Directory.GetFiles(_folder, "plan.json.unreadable-*") : [];

    /// <summary>The notice of a schema 9 file with four ContractTracker captures (D107).</summary>
    private const string FromContractTracker = "4 contracts from ContractTracker are scan missions #1–#4 now.";

    /// <summary>Martin's plan of 25 Oct 2026, schema 9, four captures (the fixture of PlanStoreTests).</summary>
    private void WriteCaptures(string path)
    {
        Directory.CreateDirectory(_folder);
        using var resource = typeof(PlanSaverTests).Assembly.GetManifestResourceStream("plans/hurston_2026-10-25.json")
            ?? throw new InvalidOperationException("Test resource 'plans/hurston_2026-10-25.json' not found.");
        using var file = File.Create(path);
        resource.CopyTo(file);
    }

    private static SavedPlan RikerDelivered()
    {
        var planned = TestPlans.SplitAndShared();
        return new SavedPlan(planned.Missions, LoadPlanner.SetDelivered(planned.Plan, HullA, "Riker", true), planned.CrateCap);
    }

    /// <summary>The start as App.ComposeStartPlan makes it: load, then the saver with what the load found.</summary>
    private (OverlayViewModel Overlay, PlanSaver Saver) Start(string? planFile = null)
    {
        var loaded = PlanStore.LoadStart(PlanPath, planFile, HullA);
        var overlay = new OverlayViewModel(HullA);
        var saver = new PlanSaver(new Window(), overlay, PlanPath, loaded.Unreadable, loaded.Notice);
        overlay.SetNotice(NoticeSource.Plan, loaded.Problem ?? loaded.Notice);
        if (loaded.Plan is { } plan)
        {
            overlay.ShowPlan(plan);
        }

        return (overlay, saver);
    }

    [Fact]
    public void The_first_save_keeps_an_unreadable_file_aside_and_writes_the_plan()
    {
        WriteBroken();

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            overlay.ShowPlan(TestPlans.SplitAndShared());
            saver.Flush();

            var kept = Kept().Should().ContainSingle().Subject;
            File.ReadAllText(kept).Should().Be(Broken);
            PlanStore.Load(PlanPath, HullA).Plan.Should().NotBeNull();
            var name = Path.GetFileName(kept);
            overlay.Notices.Should().Contain($"Previous plan.json couldn't be read; kept as {name}.");
            // The path holds this test's own folder, so parallel tests writing the same log don't matter (K10, F9).
            TestLog.Text().Should().Contain($" Plan: {PlanPath} couldn't be read; kept as {name}.");
        });
    }

    [Fact]
    public void The_second_save_keeps_nothing_aside_and_the_notice_stays()
    {
        WriteBroken();

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            overlay.ShowPlan(TestPlans.SplitAndShared());
            saver.Flush();

            overlay.ShowPlan(RikerDelivered());
            saver.Flush();

            // A second keep would have moved the plan the first save wrote.
            var kept = Kept().Should().ContainSingle().Subject;
            File.ReadAllText(PlanPath).Should().Contain("\"delivered\": true");
            overlay.Notices.Should().Contain($"Previous plan.json couldn't be read; kept as {Path.GetFileName(kept)}.");
        });
    }

    [Fact]
    public void A_locked_file_is_not_overwritten_and_kept_aside_once_it_is_free()
    {
        WriteBroken();

        Sta.Run(() =>
        {
            OverlayViewModel overlay;
            PlanSaver saver;
            // Locked as by another program: the load fails, and moving it aside fails too.
            using (new FileStream(PlanPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                (overlay, saver) = Start();
                overlay.ShowPlan(TestPlans.SplitAndShared());
                saver.Flush();

                overlay.Notices.Should().Contain(n => n.StartsWith("Plan not saved: plan.json couldn't be moved aside (", StringComparison.Ordinal));
            }

            Directory.GetFiles(_folder).Should().Equal(PlanPath);
            File.ReadAllText(PlanPath).Should().Be(Broken);

            overlay.ShowPlan(RikerDelivered());
            saver.Flush();

            File.ReadAllText(Kept().Should().ContainSingle().Subject).Should().Be(Broken);
            File.ReadAllText(PlanPath).Should().Contain("\"delivered\": true");
            overlay.Notices.Should().NotContain(n => n.StartsWith("Plan not saved", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void A_file_gone_before_the_first_save_is_written_without_a_keep()
    {
        WriteBroken();

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            File.Delete(PlanPath); // The player deleted it by hand.
            overlay.ShowPlan(TestPlans.SplitAndShared());
            saver.Flush();

            Kept().Should().BeEmpty();
            PlanStore.Load(PlanPath, HullA).Plan.Should().NotBeNull();
            overlay.Notices.Should().NotContain(n => n.Contains("plan.json", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void A_readable_file_is_saved_as_before()
    {
        PlanStore.Save(PlanPath, TestPlans.SplitAndShared());

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            overlay.ShowPlan(RikerDelivered());
            saver.Flush();

            Kept().Should().BeEmpty();
            Directory.GetFiles(_folder).Should().Equal(PlanPath);
            File.ReadAllText(PlanPath).Should().Contain("\"delivered\": true");
            overlay.Notices.Should().NotContain(n => n.Contains("plan.json", StringComparison.Ordinal) || n.StartsWith("Plan not", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void A_plan_file_given_at_the_start_keeps_an_unreadable_plan_json_aside()
    {
        WriteBroken();
        var start = Path.Combine(_folder, "start.json");
        PlanStore.Save(start, TestPlans.SplitAndShared());

        Sta.Run(() =>
        {
            // --plan: its plan is shown at the start, and that first save makes it plan.json.
            var (_, saver) = Start(start);
            saver.Flush();

            File.ReadAllText(Kept().Should().ContainSingle().Subject).Should().Be(Broken);
            PlanStore.Load(PlanPath, HullA).Plan.Should().NotBeNull();
        });
    }

    /// <summary>The find of T10.4a: since T10.3 the first scan goes into the empty plan and its save replaced the file.</summary>
    [Fact]
    public void A_scan_into_the_empty_plan_after_a_failed_load_keeps_the_file_aside()
    {
        WriteBroken();

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            overlay.Saved.Should().BeNull();

            overlay.ApplyScan(ApplyScanTests.Scanned(accepted: false));
            saver.Flush();

            File.ReadAllText(Kept().Should().ContainSingle().Subject).Should().Be(Broken);
            PlanStore.Load(PlanPath, HullA).Plan!.Missions.Should().ContainSingle();
        });
    }

    // --- The Plan line after a save: problems go, hints stay (B14) ---

    /// <summary>The find of T10.6: the write-back of a converted file cleared the D107 notice 500 ms after the start.</summary>
    [Fact]
    public void A_plan_from_before_v0_4_0_keeps_its_notice_after_the_first_save()
    {
        WriteCaptures(PlanPath);

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            saver.Flush();

            File.ReadAllText(PlanPath).Should().Contain("\"schemaVersion\": 10");
            overlay.Notices.Should().Contain(FromContractTracker);
        });
    }

    [Fact]
    public void A_failed_save_shows_its_problem_and_the_next_save_brings_the_notice_back()
    {
        WriteCaptures(PlanPath);

        Sta.Run(() =>
        {
            var (overlay, saver) = Start();
            using (new FileStream(PlanPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                saver.Flush();

                overlay.Notices.Should().Contain(n => n.StartsWith("Plan not saved (", StringComparison.Ordinal))
                    .And.NotContain(FromContractTracker);
            }

            overlay.ShowPlan(RikerDelivered());
            saver.Flush();

            overlay.Notices.Should().Contain(FromContractTracker)
                .And.NotContain(n => n.StartsWith("Plan not saved", StringComparison.Ordinal));
        });
    }

    /// <summary>--plan with captures over an unreadable plan.json: both hints, neither pushes the other out.</summary>
    [Fact]
    public void A_plan_file_with_captures_over_an_unreadable_plan_json_keeps_both_notices()
    {
        WriteBroken();
        var start = Path.Combine(_folder, "start.json");
        WriteCaptures(start);

        Sta.Run(() =>
        {
            var (overlay, saver) = Start(start);
            saver.Flush();

            var kept = Path.GetFileName(Kept().Should().ContainSingle().Subject);
            overlay.Notices.Should().Contain($"Previous plan.json couldn't be read; kept as {kept}. · {FromContractTracker}");
        });
    }
}
