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
}
