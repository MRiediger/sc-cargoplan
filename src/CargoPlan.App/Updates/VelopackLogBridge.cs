using Velopack.Logging;

namespace CargoPlan.App.Updates;

/// <summary>
/// Velopack's library lines in cargoplan.log too (T11.2, D109), as "Velopack:" from Information up: the download, the apply
/// call, the restart. <c>VelopackApp.SetLogger</c> adds it next to Velopack's own file in %LocalAppData%\velopack\, which
/// stays, and which alone has Update.exe's lines (the swap, UAC, why it failed). Lines before <see cref="Attach"/> are dropped:
/// they come from inside <c>Run()</c>, before <c>--data-dir</c> is known, and would land in the profile's log.
/// Source: Velopack.xml 1.2.161, VelopackApp.SetLogger(IVelopackLogger) "in addition to the default log file location";
/// IVelopackLogger.Log(VelopackLogLevel, string, Exception).
/// </summary>
public sealed class VelopackLogBridge : IVelopackLogger
{
    private const string LogContext = "Velopack";

    private volatile bool _attached;

    /// <summary>From the start's step that sets the data folder on (App.OnStartup).</summary>
    public void Attach() => _attached = true;

    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
    {
        if (!_attached || logLevel < VelopackLogLevel.Information)
        {
            return;
        }

        AppLog.Write(LogContext, exception is null ? $"{logLevel}: {message}" : $"{logLevel}: {message} ({exception.GetType().Name}: {exception.Message})");
    }
}
