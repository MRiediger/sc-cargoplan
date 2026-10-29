using System.IO;
using CargoPlan.App.ContractTracker;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Import.ContractTracker;
using Microsoft.Data.Sqlite;

namespace CargoPlan.App.Tests;

public sealed class ContractTrackerSyncTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"cargoplan-sync-{Guid.NewGuid():N}");

    public ContractTrackerSyncTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string DatabasePath => Path.Combine(_folder, "contracts.db");

    private static ContractTrackerCandidate Candidate(long id, string destination) =>
        new([id], null, [new MissionStop(destination, 1, [new MissionItem("Stims", 4, CrateSize.Four)])], false, false, null, null, null);

    private static OverlayViewModel Overlay() => new(ShipLoader.Load("hull_a"));

    // --- File state ---

    [Fact]
    public void File_state_is_equal_while_nothing_changes()
    {
        File.WriteAllText(DatabasePath, "db");

        ContractTrackerFileState.Of(DatabasePath).Should().Be(ContractTrackerFileState.Of(DatabasePath));
    }

    [Fact]
    public void File_state_changes_when_the_wal_is_written_created_or_deleted()
    {
        File.WriteAllText(DatabasePath, "db");
        var before = ContractTrackerFileState.Of(DatabasePath);

        File.WriteAllText(DatabasePath + "-wal", "frame");
        var created = ContractTrackerFileState.Of(DatabasePath);
        File.AppendAllText(DatabasePath + "-wal", " another frame");
        var written = ContractTrackerFileState.Of(DatabasePath);
        File.Delete(DatabasePath + "-wal");

        created.Should().NotBe(before);
        written.Should().NotBe(created);
        ContractTrackerFileState.Of(DatabasePath).Should().Be(before);
    }

    [Fact]
    public void File_state_ignores_the_shared_memory_file()
    {
        // Our own read creates -shm (D38); it never means news.
        File.WriteAllText(DatabasePath, "db");
        var before = ContractTrackerFileState.Of(DatabasePath);

        File.WriteAllText(DatabasePath + "-shm", "index");

        ContractTrackerFileState.Of(DatabasePath).Should().Be(before);
    }

    [Fact]
    public void File_state_of_a_missing_database_is_that_of_no_files()
    {
        ContractTrackerFileState.Of(DatabasePath).Should().Be(default(ContractTrackerFileState));
    }

    // --- Sync ---

    [Fact]
    public async Task SyncAsync_hands_the_read_to_the_overlay_with_its_watermark_and_known_ids()
    {
        var overlay = Overlay();
        List<(long Watermark, long[] Known)> asked = [];
        using var sync = new ContractTrackerSync(overlay, DatabasePath, (_, watermark, known) =>
        {
            asked.Add((watermark, [.. known]));
            return new ContractTrackerReadResult([Candidate(watermark + 1, "Riker")], null, new HashSet<long>(known));
        });

        await sync.SyncAsync();
        await sync.SyncAsync();

        asked.Select(a => a.Watermark).Should().Equal(0, 1);
        asked[1].Known.Should().Equal(1);
        overlay.Missions.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncAsync_of_a_file_that_is_no_database_shows_a_notice()
    {
        var overlay = Overlay();
        using var sync = new ContractTrackerSync(overlay, DatabasePath, (_, _, _) => throw new SqliteException("file is not a database", 26));

        await sync.SyncAsync();

        overlay.Notices.Should().ContainSingle().Which.Should().StartWith(ContractTrackerSync.NotReadablePrefix);
    }

    [Fact]
    public async Task SyncAsync_of_a_missing_database_shows_nothing()
    {
        // The real reader: a missing file reads nothing, and the watcher says nothing either; players without
        // ContractTracker see the hint in the settings window only (D44).
        var overlay = Overlay();
        using var sync = new ContractTrackerSync(overlay, DatabasePath);

        await sync.SyncAsync();

        overlay.Notices.Should().BeEmpty();
        overlay.HasPlan.Should().BeFalse();
    }

    [Fact]
    public async Task The_button_or_hotkey_on_a_missing_database_says_where_to_set_the_path()
    {
        var overlay = Overlay();
        using var sync = new ContractTrackerSync(overlay, DatabasePath);

        await sync.SyncAsync(manual: true);

        overlay.Notices.Should().Equal(ContractTrackerSync.NotFoundText(DatabasePath));
        ContractTrackerSync.NotFoundText(DatabasePath).Should().Be($"ContractTracker not found at {DatabasePath} – set the path in the settings.");
    }

    // --- Settings (T5.5, D44) ---

    [Fact]
    public async Task Auto_sync_off_shows_only_the_count_and_the_button_takes_them_in()
    {
        File.WriteAllText(DatabasePath, "db");
        var overlay = Overlay();
        using var sync = new ContractTrackerSync(
            overlay, DatabasePath, (_, watermark, _) => new ContractTrackerReadResult(
                [.. new[] { 36L, 37L }.Where(id => id > watermark).Select(id => Candidate(id, $"Stop {id}"))], null, new HashSet<long>()),
            autoSync: false);

        await sync.SyncAsync();

        overlay.HasPlan.Should().BeFalse();
        overlay.Notices.Should().Equal(OverlayViewModel.NewContractsText(2));

        await sync.SyncAsync(manual: true);

        overlay.Missions.Should().HaveCount(2);
        overlay.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Configure_turns_auto_sync_on_and_reads_the_new_path()
    {
        var other = Path.Combine(_folder, "other.db");
        var overlay = Overlay();
        List<string> paths = [];
        using var sync = new ContractTrackerSync(
            overlay, DatabasePath, (path, _, _) =>
            {
                paths.Add(path);
                return new ContractTrackerReadResult([Candidate(36, "Riker")], null, new HashSet<long>());
            },
            autoSync: false);
        await sync.SyncAsync();

        sync.Configure(other, autoSync: true);
        await sync.SyncAsync();

        paths.Should().Equal(DatabasePath, other);
        overlay.Missions.Should().ContainSingle();
    }

    [Fact]
    public async Task A_sync_asked_for_while_reading_runs_once_more_afterwards()
    {
        var overlay = Overlay();
        var reads = 0;
        TaskCompletionSource firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sync = new ContractTrackerSync(overlay, DatabasePath, (_, _, _) =>
        {
            if (Interlocked.Increment(ref reads) == 1)
            {
                firstRead.Task.Wait();
            }

            return new ContractTrackerReadResult([], null);
        });

        var first = sync.SyncAsync();
        var second = sync.SyncAsync();
        var third = sync.SyncAsync();
        firstRead.SetResult();
        await Task.WhenAll(first, second, third);

        reads.Should().Be(2, "one read at a time, and the requests made meanwhile need one more, not two");
    }

    // --- Safety net (D82) ---

    /// <summary>A reader that fails while <see cref="Fail"/> is set, counting its reads; the first success brings a mission.</summary>
    private sealed class FlakyReader
    {
        public bool Fail { get; set; } = true;

        public int Reads { get; private set; }

        public List<Exception> Logged { get; } = [];

        public ContractTrackerReadResult Read(string path, long watermark, IReadOnlyCollection<long> known)
        {
            Reads++;
            return Fail
                ? throw new InvalidOperationException("boom")
                : new ContractTrackerReadResult([.. new[] { 36L }.Where(id => id > watermark).Select(id => Candidate(id, "Riker"))], null, new HashSet<long>());
        }

        public void Log(string context, Exception error) => Logged.Add(error);
    }

    private ContractTrackerSync Sync(OverlayViewModel overlay, FlakyReader reader) =>
        new(overlay, DatabasePath, reader.Read, log: reader.Log);

    // ContractTracker wrote: the files differ from the last read.
    private void Write() => File.AppendAllText(DatabasePath, "x");

    private async Task FailTicks(ContractTrackerSync sync, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Write();
            await sync.TickAsync();
        }
    }

    [Fact]
    public async Task A_tick_with_a_thrown_exception_keeps_the_plan_and_the_app()
    {
        var overlay = Overlay();
        FlakyReader reader = new() { Fail = false };
        using var sync = Sync(overlay, reader);
        Write();
        await sync.TickAsync();
        var before = overlay.Saved;

        reader.Fail = true;
        Write();
        var tick = () => sync.TickAsync();

        await tick.Should().NotThrowAsync();
        before.Should().NotBeNull();
        overlay.Saved.Should().BeSameAs(before);
        overlay.Notices.Should().Equal(ContractTrackerSync.FailedText(new InvalidOperationException("boom")));
        overlay.Notices[0].Should().Be("ContractTracker sync failed: InvalidOperationException: boom – details in cargoplan.log");
        reader.Logged.Should().ContainSingle().Which.StackTrace.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_same_failure_shows_its_notice_once_until_a_sync_succeeds()
    {
        var overlay = Overlay();
        FlakyReader reader = new();
        using var sync = Sync(overlay, reader);
        await FailTicks(sync, 1);

        overlay.ShowContractTrackerProblem("x");
        await FailTicks(sync, 1);

        overlay.Notices.Should().Equal("x");
        reader.Logged.Should().HaveCount(2, "the log gets every failure");

        reader.Fail = false;
        await FailTicks(sync, 1);
        reader.Fail = true;
        await FailTicks(sync, 1);

        overlay.Notices.Should().Equal(ContractTrackerSync.FailedText(new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task The_fifth_failure_in_a_row_pauses_the_sync()
    {
        var overlay = Overlay();
        FlakyReader reader = new();
        using var sync = Sync(overlay, reader);

        await FailTicks(sync, ContractTrackerSync.MaxFailures);

        overlay.Notices.Should().Equal(ContractTrackerSync.PausedText);
        ContractTrackerSync.PausedText.Should().Be("Sync paused – check ContractTracker file");

        await sync.TickAsync();
        reader.Reads.Should().Be(ContractTrackerSync.MaxFailures, "paused: the files are as they were");

        Write();
        await sync.TickAsync();
        reader.Reads.Should().Be(ContractTrackerSync.MaxFailures + 1, "ContractTracker wrote: the tick tries again");
    }

    [Fact]
    public async Task A_success_resets_the_failure_count()
    {
        var overlay = Overlay();
        FlakyReader reader = new();
        using var sync = Sync(overlay, reader);
        await FailTicks(sync, ContractTrackerSync.MaxFailures - 1);

        reader.Fail = false;
        await FailTicks(sync, 1);
        reader.Fail = true;
        await FailTicks(sync, ContractTrackerSync.MaxFailures - 1);

        overlay.Notices.Should().Equal(ContractTrackerSync.FailedText(new InvalidOperationException("boom")));
        reader.Reads.Should().Be(2 * ContractTrackerSync.MaxFailures - 1);
    }

    [Fact]
    public async Task A_manual_sync_lifts_the_pause_only_when_it_succeeds()
    {
        var overlay = Overlay();
        FlakyReader reader = new();
        using var sync = Sync(overlay, reader);
        await FailTicks(sync, ContractTrackerSync.MaxFailures);

        var manual = () => sync.RunSyncGuardedAsync(manual: true);
        await manual.Should().NotThrowAsync();
        await sync.TickAsync();

        reader.Reads.Should().Be(ContractTrackerSync.MaxFailures + 1, "the button read, the tick stayed paused");

        reader.Fail = false;
        await sync.RunSyncGuardedAsync(manual: true);
        reader.Fail = true;
        await FailTicks(sync, 1);
        await sync.TickAsync();

        overlay.Notices.Should().Equal(ContractTrackerSync.FailedText(new InvalidOperationException("boom")));
        reader.Reads.Should().Be(ContractTrackerSync.MaxFailures + 4, "no pause after the success: the last tick read again");
    }
}
