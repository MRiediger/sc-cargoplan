using System.IO;

namespace CargoPlan.App.Tests;

public sealed class AppLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"cargoplan-log-{Guid.NewGuid():N}");

    public AppLogTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string LogPath => Path.Combine(_folder, "cargoplan.log");

    private static Exception Thrown()
    {
        try
        {
            throw new InvalidOperationException("boom", new IOException("inner"));
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    [Fact]
    public void An_entry_holds_the_context_and_the_whole_exception()
    {
        var error = Thrown();

        AppLog.WriteTo(LogPath, "ContractTracker sync", error);
        AppLog.WriteTo(LogPath, "Unhandled", error);

        var text = File.ReadAllText(LogPath);
        text.Should().Contain("ContractTracker sync").And.Contain("Unhandled");
        text.Should().Contain(error.ToString(), "type, message, inner exception and stack trace");
    }

    [Fact]
    public void A_full_log_moves_to_the_older_file()
    {
        File.WriteAllText(LogPath, new string('x', (int)AppLog.MaxBytes));
        File.WriteAllText(LogPath + ".1", "oldest");

        AppLog.WriteTo(LogPath, "Unhandled", Thrown());

        new FileInfo(LogPath + ".1").Length.Should().Be(AppLog.MaxBytes, "the full log replaced the older one");
        File.ReadAllText(LogPath).Should().Contain("boom").And.NotContain("xxx");
    }

    [Fact]
    public void A_log_that_cannot_be_written_never_throws()
    {
        // A folder where the file should be: AppendAllText throws UnauthorizedAccessException.
        Directory.CreateDirectory(LogPath);

        var write = () => AppLog.WriteTo(LogPath, "Unhandled", Thrown());

        write.Should().NotThrow();
    }
}
