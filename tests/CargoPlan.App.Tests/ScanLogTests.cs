using CargoPlan.App.Scanning;

namespace CargoPlan.App.Tests;

/// <summary>scans.log (T10.2b2, D103): the last 50 reads with raw text and result, never a crash, never the player's profile here.</summary>
public sealed class ScanLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string LogPath => Path.Combine(_folder, "scans.log");

    [Fact]
    public void An_entry_holds_the_result_and_the_raw_text()
    {
        new ScanLog(LogPath).Write("DETAILS\nDeliver 0/8 SCU of Stims to Riker.", "Added #1");

        var lines = File.ReadAllLines(LogPath);
        lines[0].Should().StartWith("=== ").And.EndWith(" Added #1");
        lines[1..].Should().Equal("DETAILS", "Deliver 0/8 SCU of Stims to Riker.");
    }

    [Fact]
    public void The_log_keeps_the_last_50_entries()
    {
        var log = new ScanLog(LogPath);

        for (var i = 1; i <= ScanLog.MaxEntries + 1; i++)
        {
            log.Write($"raw {i}", $"Added #{i}");
        }

        var results = File.ReadAllLines(LogPath).Where(l => l.StartsWith("=== ", StringComparison.Ordinal)).ToList();
        results.Should().HaveCount(ScanLog.MaxEntries);
        results[0].Should().EndWith(" Added #2");
        results[^1].Should().EndWith(" Added #51");
    }

    [Fact]
    public void A_log_that_cant_be_written_never_throws()
    {
        // A folder where the file should be: logging never takes the scan down (D82).
        Directory.CreateDirectory(LogPath);

        var act = () => new ScanLog(LogPath).Write("raw", "Added #1");

        act.Should().NotThrow();
    }

    [Fact]
    public void The_default_log_lies_next_to_the_plan()
    {
        ScanLog.DefaultPath.Should().Be(Path.Combine(AppFolders.LocalRoot, "scans.log"));
    }
}
