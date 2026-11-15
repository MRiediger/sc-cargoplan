using System.IO;
using System.Net.Http;
using System.Net.Sockets;

namespace CargoPlan.App.Updates;

/// <summary>
/// The short reason on the update line when a download or an apply fails (T11.2, U7, D109). Four texts only: the player can
/// act on these; the exception itself goes to cargoplan.log. Which exception means what is taken from Velopack 1.2.161
/// (UpdateManager.cs 219 ff., HttpClientFileDownloader.cs 55–82, LockFile.cs 47–49, SimpleFileSource.cs 78).
/// </summary>
public static class UpdateFailure
{
    /// <summary>The portable folder can't be written (e.g. below Program Files) and Update.exe couldn't swap it.</summary>
    public const string FolderNotWritable = "folder not writable";

    /// <summary>No answer from the source: offline, DNS, refused, a timeout; or a local feed folder that is gone.</summary>
    public const string NoNetwork = "no network";

    /// <summary>The UAC prompt for the swap was declined (seen only after Update.exe started the old app again).</summary>
    public const string Cancelled = "cancelled";

    /// <summary>Everything else: a broken package, a release without its file (HTTP 404), the rate limit (403), Update.exe missing.</summary>
    public const string SeeLog = "see cargoplan.log";

    // Source: https://learn.microsoft.com/windows/win32/seccrypto/common-hresult-values (E_ACCESSDENIED)
    private const int E_ACCESSDENIED = unchecked((int)0x80070005);

    /// <param name="localFeedMissing">
    /// <c>--updates-source</c> and its folder is gone: SimpleFileSource then throws a bare <see cref="Exception"/>; for a gate
    /// the folder is the network.
    /// </param>
    /// <remarks>The app closing (our own token) is told apart by the caller before; a cancellation here is HttpClient's timeout.</remarks>
    public static string Classify(Exception error, bool localFeedMissing = false)
    {
        if (localFeedMissing)
        {
            return NoNetwork;
        }

        // Velopack wraps: AcquireLockFailedException(inner), HttpRequestException(inner SocketException).
        for (var current = error; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case UnauthorizedAccessException:
                case IOException { HResult: E_ACCESSDENIED }:
                    return FolderNotWritable;

                // Only an answer has a status code; without one the request never got through.
                // Source: https://learn.microsoft.com/dotnet/api/system.net.http.httprequestexception.statuscode
                case HttpRequestException { StatusCode: null }:
                case SocketException:
                case OperationCanceledException:
                    return NoNetwork;

                case HttpRequestException:
                    return SeeLog;
            }
        }

        return SeeLog;
    }
}
