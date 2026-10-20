using System.IO;

namespace CargoPlan.App.ContractTracker;

/// <summary>
/// What the files of ContractTracker's database look like on disk: the database and its write-ahead log, each with
/// existence, length and last write. Equal before and after an event = nothing new, e.g. the -wal and -shm our own read
/// leaves (D38); the -shm is no part of it. Diff on file level, before any read (T5.4).
/// </summary>
public readonly record struct ContractTrackerFileState(ContractTrackerFileState.Stamp Database, ContractTrackerFileState.Stamp Wal)
{
    public const string WalSuffix = "-wal";

    /// <summary>One file; default = missing.</summary>
    public readonly record struct Stamp(bool Exists, long Length, DateTime LastWriteUtc)
    {
        public static Stamp Of(string path)
        {
            // FileInfo reads the file system when created; a missing file is no exception, Exists is false.
            FileInfo info = new(path);
            return info.Exists ? new Stamp(true, info.Length, info.LastWriteTimeUtc) : default;
        }
    }

    public static ContractTrackerFileState Of(string databasePath) =>
        new(Stamp.Of(databasePath), Stamp.Of(databasePath + WalSuffix));
}
