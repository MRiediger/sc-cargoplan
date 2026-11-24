// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using CargoPlan.App.Plans;
using CargoPlan.Core.Routing;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Routing;

/// <summary>
/// The observations of the run and routes.json (T13.1, U3, D118 addendum): every scan read whole, with title and reward,
/// is recorded and written at once, whatever tab is selected. The file boundary for routes.json, as PlanStore is for
/// plan.json: an unreadable file is kept aside by the first save, never overwritten (D106). The player never sees the
/// file, so its problems go to cargoplan.log only, never to the notice line. Scans come one at a time; the lock is for
/// the reader on another thread.
/// </summary>
internal sealed class RoutesBook
{
    private const string LogContext = "Routes";

    private readonly Lock _lock = new();
    private readonly string _path;
    private readonly Positions _positions;
    private Observations _observations;
    private bool _keepUnreadable;

    private RoutesBook(string path, Positions positions, Observations observations, bool keepUnreadable, long loadMilliseconds = 0) =>
        (_path, _positions, _observations, _keepUnreadable, LoadMilliseconds) = (path, positions, observations.Named(positions), keepUnreadable, loadMilliseconds);

    /// <summary>How long reading and folding the file took at the start (T13.3).</summary>
    public long LoadMilliseconds { get; }

    /// <summary>%LocalAppData%\SC-CargoPlan\routes.json, next to plan.json (D118).</summary>
    public static string DefaultPath => Path.Combine(AppFolders.LocalRoot, "routes.json");

    /// <summary>
    /// After a scan was recorded, on the scan's thread: all observations and the entry of the scan's variant. Not under the
    /// lock: a listener may read <see cref="Observations"/>.
    /// </summary>
    public event Action<Observations, Observation>? Recorded;

    public Observations Observations
    {
        get
        {
            lock (_lock)
            {
                return _observations;
            }
        }
    }

    /// <summary>The observations in <paramref name="path"/>; none for a missing file, and none for one that can't be read,
    /// which stays as it is until the first save keeps it aside. Each place as one of <paramref name="positions"/> names
    /// it (E3 of T13.3a), in memory: the file gets those names with the next save, never by loading.</summary>
    /// <param name="positions">The hauling data's positions; <see cref="Positions.Empty"/> without hauling data, then names stay as read.</param>
    public static RoutesBook Load(string path, Positions positions)
    {
        if (!File.Exists(path))
        {
            return new(path, positions, Observations.Empty, keepUnreadable: false);
        }

        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using var stream = File.OpenRead(path);
            var read = RoutesFile.Read(stream, path);
            foreach (var dropped in read.Dropped)
            {
                AppLog.Write(LogContext, dropped);
            }

            return new(path, positions, read.Observations, keepUnreadable: false, watch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is RoutesFormatException or IOException or UnauthorizedAccessException)
        {
            AppLog.Write(LogContext, $"Routes not loaded ({ex.Message}). The next save keeps the file aside. [{ex.GetType().Name}]");
            return new(path, positions, Observations.Empty, keepUnreadable: true);
        }
    }

    /// <summary>
    /// <paramref name="scanned"/> as an observation, saved; nothing for a scan that isn't one. Never throws: a file that
    /// can't be written costs neither the scan nor the plan, the observations stay in memory for the next save.
    /// </summary>
    public void Observe(ScannedContract scanned)
    {
        if (scanned.ToObservation(DateTime.UtcNow) is not { } observed)
        {
            // Only a contract that could have been one is worth a line: an offer or accepted contract read whole.
            if (scanned.View != ContractView.Unknown && !scanned.InHardDoubt && scanned.Stops.Count > 0)
            {
                AppLog.Write(LogContext, $"not observed: {scanned.NotObservedReason}");
            }

            return;
        }

        Observations all;
        lock (_lock)
        {
            _observations = all = _observations.Record(observed).Named(_positions);
            Save();
        }

        Recorded?.Invoke(all, all.Entries.Single(e => e.Key == observed.Key));
    }

    private void Save()
    {
        try
        {
            if (_keepUnreadable)
            {
                if (PlanStore.KeepUnreadable(_path, DateTime.Now) is { } keptAs)
                {
                    AppLog.Write(LogContext, $"{_path} couldn't be read; kept as {keptAs}.");
                }

                _keepUnreadable = false;
            }

            AtomicFile.Write(_path, stream => RoutesFile.Write(stream, _observations));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write(LogContext, $"{_path} not saved ({ex.Message}). [{ex.GetType().Name}]");
        }
    }
}
