using System.IO;
using System.Windows.Threading;
using CargoPlan.App.ViewModels;
using CargoPlan.Import.ContractTracker;
using Microsoft.Data.Sqlite;

namespace CargoPlan.App.ContractTracker;

/// <summary>
/// Takes ContractTracker captures into the plan without a click (T5.4): watches the database and its write-ahead log,
/// waits until writing rests, reads off the UI thread and hands the result to the overlay in one place
/// (<see cref="OverlayViewModel.ApplyContractTracker"/>). The editor's "Sync from ContractTracker" runs the same read.
/// </summary>
public sealed class ContractTrackerSync : IDisposable
{
    /// <summary>One capture is one transaction within about 40 ms (D38); 500 ms catches it whole.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    public const string NotReadablePrefix = "ContractTracker database not readable: ";

    private readonly OverlayViewModel _viewModel;
    private readonly string _databasePath;
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
    public ContractTrackerSync(
        OverlayViewModel viewModel, string databasePath, Func<string, long, IReadOnlyCollection<long>, ContractTrackerReadResult>? read = null)
    {
        _viewModel = viewModel;
        _databasePath = databasePath;
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

        _timer.Start();
    }

    /// <summary>
    /// Reads above the watermark and hands the result to the overlay; one read at a time, a request during a read runs
    /// one more afterwards. Called on the UI thread; the reader runs on the thread pool, the result comes back here
    /// (await resumes in WPF's synchronization context). A file that is no database gives a notice.
    /// </summary>
    /// <param name="manual">The editor's button: syncs even without news, which places waiting missions that fit now.</param>
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
                try
                {
                    var result = await Task.Run(() => _read(_databasePath, watermark, known));
                    _viewModel.ApplyContractTracker(result, manual);
                }
                catch (SqliteException ex)
                {
                    // The path names a file that is no database (or a broken one); a missing file never gets here.
                    _viewModel.ShowContractTrackerProblem(NotReadablePrefix + ex.Message);
                }

                _afterLastRead = ContractTrackerFileState.Of(_databasePath);
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
