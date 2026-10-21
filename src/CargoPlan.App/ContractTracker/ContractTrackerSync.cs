using System.IO;
using System.Windows.Threading;
using CargoPlan.App.ViewModels;
using CargoPlan.Import.ContractTracker;
using Microsoft.Data.Sqlite;

namespace CargoPlan.App.ContractTracker;

/// <summary>
/// Takes ContractTracker captures into the plan without a click (T5.4): watches the database and its write-ahead log,
/// waits until writing rests, reads off the UI thread and hands the result to the overlay in one place
/// (<see cref="OverlayViewModel.ApplyContractTracker"/>). The editor's "Sync from ContractTracker" and its hotkey run the
/// same read. With auto-sync off, a read only counts what is new (D44).
/// </summary>
public sealed class ContractTrackerSync : IDisposable
{
    /// <summary>One capture is one transaction within about 40 ms (D38); 500 ms catches it whole.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    public const string NotReadablePrefix = "ContractTracker database not readable: ";

    /// <summary>Button or hotkey without a database: the player asked, so the overlay answers (D44). The watcher stays silent.</summary>
    public static string NotFoundText(string databasePath) =>
        $"ContractTracker not found at {databasePath} – set the path in the settings.";

    private readonly OverlayViewModel _viewModel;
    private string _databasePath;
    private bool _autoSync;
    private readonly Func<string, long, IReadOnlyCollection<long>, ContractTrackerReadResult> _read;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _timer = new() { Interval = Debounce };
    private FileSystemWatcher? _watcher;

    // The files after our last read: an event that leaves them so is our own read or a checkpoint without news.
    private ContractTrackerFileState? _afterLastRead;
    private bool _reading;
    private bool _pending;

    /// <param name="read">The reader; <see cref="ContractTrackerReader.Read(string, long, IReadOnlyCollection{long})"/> by
    /// default, replaced in tests.</param>
    /// <param name="autoSync">false = the watcher only counts new captures (D44).</param>
    public ContractTrackerSync(
        OverlayViewModel viewModel,
        string databasePath,
        Func<string, long, IReadOnlyCollection<long>, ContractTrackerReadResult>? read = null,
        bool autoSync = true)
    {
        _viewModel = viewModel;
        _databasePath = databasePath;
        _autoSync = autoSync;
        _read = read ?? ContractTrackerReader.Read;
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// Watches the database folder and reads once after the debounce, so captures made while the overlay was closed come
    /// in. Without the folder (ContractTracker not installed) there is nothing to watch and nothing to say; the button
    /// still reads.
    /// </summary>
    public void Start()
    {
        Watch();
        _timer.Start();
    }

    /// <summary>
    /// From the settings window (D44), without a restart: a new path gets a watcher of its own; either way the next read
    /// comes after the debounce, as at the start, and reads even if the files look as before, so turning auto-sync on
    /// takes in what waited. Not awaited here: the read runs from the timer like any other.
    /// </summary>
    public void Configure(string databasePath, bool autoSync)
    {
        if (!string.Equals(databasePath, _databasePath, StringComparison.OrdinalIgnoreCase))
        {
            _databasePath = databasePath;
            _watcher?.Dispose();
            _watcher = null;
            Watch();
        }

        _autoSync = autoSync;
        _afterLastRead = null;
        Restart();
    }

    private void Watch()
    {
        if (Path.GetDirectoryName(_databasePath) is { } folder && Directory.Exists(folder))
        {
            // Source: https://learn.microsoft.com/dotnet/api/system.io.filesystemwatcher
            _watcher = new FileSystemWatcher(folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.Error += OnWatcherError;
            _watcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>
    /// Reads above the watermark and hands the result to the overlay; one read at a time, a request during a read runs
    /// one more afterwards. Called on the UI thread; the reader runs on the thread pool, the result comes back here
    /// (await resumes in WPF's synchronization context). A file that is no database gives a notice.
    /// </summary>
    /// <param name="manual">The editor's button or the hotkey: syncs even without news, which places waiting missions that
    /// fit now, and with auto-sync off too; a missing database gives a notice.</param>
    public async Task SyncAsync(bool manual = false)
    {
        if (_reading)
        {
            _pending = true;
            return;
        }

        _reading = true;
        try
        {
            do
            {
                _pending = false;
                var (watermark, known) = _viewModel.ContractTrackerQuery();

                // Taken before the pool thread starts: Configure may change the field meanwhile, the next round reads there.
                var path = _databasePath;
                try
                {
                    if (manual && !File.Exists(path))
                    {
                        _viewModel.ShowContractTrackerProblem(NotFoundText(path));
                    }
                    else
                    {
                        var result = await Task.Run(() => _read(path, watermark, known));
                        if (manual || _autoSync)
                        {
                            _viewModel.ApplyContractTracker(result, manual);
                        }
                        else
                        {
                            _viewModel.ShowNewContracts(result);
                        }
                    }
                }
                catch (SqliteException ex)
                {
                    // The path names a file that is no database (or a broken one); a missing file never gets here.
                    _viewModel.ShowContractTrackerProblem(NotReadablePrefix + ex.Message);
                }

                _afterLastRead = ContractTrackerFileState.Of(path);
            }
            while (_pending);
        }
        finally
        {
            _reading = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _watcher?.Dispose();
    }

    /// <summary>On a pool thread: only the database and its log count; the -shm and ContractTracker's other files don't.</summary>
    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        var name = Path.GetFileName(_databasePath);
        if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Name, name + ContractTrackerFileState.WalSuffix, StringComparison.OrdinalIgnoreCase)
            || (e is RenamedEventArgs renamed && string.Equals(renamed.OldName, name + ContractTrackerFileState.WalSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            _dispatcher.BeginInvoke(Restart);
        }
    }

    /// <summary>Events got lost (buffer overflow): look once, the file state decides whether there is news.</summary>
    private void OnWatcherError(object sender, ErrorEventArgs e) => _dispatcher.BeginInvoke(Restart);

    private void Restart()
    {
        _timer.Stop();
        _timer.Start();
    }

    // async void: an event handler (CLAUDE.md). SyncAsync handles the expected failure; anything else is a bug and surfaces.
    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (ContractTrackerFileState.Of(_databasePath) == _afterLastRead)
        {
            return;
        }

        await SyncAsync();
    }
}
