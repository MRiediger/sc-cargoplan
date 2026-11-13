using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using CargoPlan.App.Updates;
using Velopack.Exceptions;

namespace CargoPlan.App.Tests;

/// <summary>
/// U7 (T11.2, D109): what the update line says when a download or an apply fails, by the exception Velopack 1.2.161 throws
/// (source at the tag: UpdateManager.cs 219 ff., HttpClientFileDownloader.cs 55–82, LockFile.cs 47–49, SimpleFileSource.cs 78).
/// </summary>
public class UpdateFailureTests
{
    /// <summary>Velopack's own wrapper of a failed lock; its constructors are internal in 1.2.161, so as Velopack makes it.</summary>
    private static Exception LockFailed(Exception inner) =>
        (Exception)Activator.CreateInstance(
            typeof(AcquireLockFailedException), BindingFlags.Instance | BindingFlags.NonPublic, null, [inner], CultureInfo.InvariantCulture)!;

    [Fact]
    public void No_access_is_a_folder_not_writable()
    {
        UpdateFailure.Classify(new UnauthorizedAccessException("Access to the path is denied.")).Should().Be(UpdateFailure.FolderNotWritable);
    }

    [Fact]
    public void A_lock_file_that_cant_be_written_is_a_folder_not_writable()
    {
        // packages\ exists but is read-only: the lock throws, Velopack wraps it (LockFile.cs 47–49).
        var error = LockFailed(new UnauthorizedAccessException("denied"));

        UpdateFailure.Classify(error).Should().Be(UpdateFailure.FolderNotWritable);
    }

    [Fact]
    public void An_io_error_with_access_denied_is_a_folder_not_writable()
    {
        UpdateFailure.Classify(new IOException("denied", unchecked((int)0x80070005))).Should().Be(UpdateFailure.FolderNotWritable);
    }

    [Fact]
    public void A_lock_held_by_another_update_is_for_the_log()
    {
        var error = LockFailed(new IOException("The process cannot access the file", unchecked((int)0x80070020)));

        UpdateFailure.Classify(error).Should().Be(UpdateFailure.SeeLog);
    }

    [Fact]
    public void A_request_without_an_answer_is_no_network()
    {
        UpdateFailure.Classify(new HttpRequestException("No such host is known.", new SocketException(11001))).Should().Be(UpdateFailure.NoNetwork);
    }

    [Fact]
    public void An_http_answer_is_a_release_problem_for_the_log_not_the_network()
    {
        // 403 rate limit or 404 missing asset: the network works, the release doesn't (T11.3 hygiene).
        UpdateFailure.Classify(new HttpRequestException("Not Found", null, HttpStatusCode.NotFound)).Should().Be(UpdateFailure.SeeLog);
    }

    [Fact]
    public void An_http_timeout_is_no_network()
    {
        // HttpClient's own timeout is a TaskCanceledException; the app closing is told apart before (UpdateService).
        UpdateFailure.Classify(new TaskCanceledException("timeout", new TimeoutException())).Should().Be(UpdateFailure.NoNetwork);
    }

    [Fact]
    public void A_local_feed_that_went_missing_is_no_network()
    {
        // SimpleFileSource throws a bare Exception (SimpleFileSource.cs 78); for --updates-source the folder is the network.
        UpdateFailure.Classify(new Exception("The file 'x' does not exist. The packages directory is invalid."), localFeedMissing: true)
            .Should().Be(UpdateFailure.NoNetwork);
    }

    [Theory]
    [MemberData(nameof(ForTheLog))]
    public void Everything_else_is_for_the_log(Exception error)
    {
        UpdateFailure.Classify(error).Should().Be(UpdateFailure.SeeLog);
    }

    public static TheoryData<Exception> ForTheLog() =>
    [
        new ChecksumFailedException("CargoPlan-0.9.1-full.nupkg", "SHA256 doesn't match"),
        new FileNotFoundException("Update.exe not found"),
        new Exception("The file 'x' does not exist. The packages directory is invalid."),
        new InvalidOperationException("odd"),
    ];
}
