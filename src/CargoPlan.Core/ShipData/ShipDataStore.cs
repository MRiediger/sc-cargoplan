using System.IO.Compression;
using System.Security.Cryptography;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.ShipData;

/// <summary>Where the active ship data come from: the release ZIP next to the exe, or a data release downloaded later.</summary>
public enum ShipDataOrigin
{
    Baseline,
    Downloaded,
}

/// <summary>The ship data the app loads at this start (T12.3): one folder of ship files and one outline file.</summary>
/// <param name="Folder">The data's own folder: next to the exe, or <c>ships\&lt;tag&gt;\</c> in the data folder.</param>
public sealed record ActiveShipData(ShipDataVersion Version, ShipDataOrigin Origin, string Folder, string ShipsFolder, string OutlinesPath)
{
    /// <summary>The ships and outlines next to the running exe (D56, D62): the release's baseline.</summary>
    public static ActiveShipData NextToExe { get; } = new(
        ShipDataVersion.Baseline, ShipDataOrigin.Baseline, AppContext.BaseDirectory, ShipLoader.DefaultFolder, ShipOutlineLoader.DefaultPath);

    /// <summary>One line for the log and the CLI: <c>ships-2026.11.1 (downloaded) C:\…\ships\ships-2026.11.1</c>.</summary>
    public override string ToString() => $"{Version} ({(Origin == ShipDataOrigin.Baseline ? "baseline" : "downloaded")}) {Folder}";
}

/// <summary>
/// The downloaded data releases under <c>&lt;data folder&gt;\ships\&lt;tag&gt;\</c> (T12.3, D112): which one is active, how a
/// release gets there, and what goes at the start. Files only; the download is the app's. A folder counts once its
/// manifest.json is in it, and the manifest is the last file of an install, written before the folder gets its name.
/// </summary>
/// <param name="root">The folder of the releases, <see cref="RootIn"/> of the data folder.</param>
/// <param name="baseline">The data next to the exe; active when no download is readable and at least as new.</param>
/// <param name="maxUnpackedBytes">A release that unpacks to more is refused (a zip bomb, or not our release).</param>
public sealed class ShipDataStore(string root, ActiveShipData baseline, long maxUnpackedBytes = ShipDataStore.MaxUnpackedBytes)
{
    /// <summary>The folder's name in the data folder, next to plan.json and holo\.</summary>
    public const string FolderName = "ships";

    public const string ManifestFileName = ShipDataManifestReader.FileName;

    /// <summary>64 MiB; ships-2026.11.1 unpacks to about 1 MB.</summary>
    public const long MaxUnpackedBytes = 64L * 1024 * 1024;

    private const string TempSuffix = ".tmp";

    public static string RootIn(string dataFolder) => Path.Combine(dataFolder, FolderName);

    /// <summary>
    /// The highest folder <c>ships-…</c> whose manifest.json reads, names that folder's tag and a schema
    /// <see cref="ShipLoader"/> reads, and that is no older than the baseline; else the baseline. Never throws: a folder
    /// it can't read doesn't count.
    /// </summary>
    public ActiveShipData Active()
    {
        ActiveShipData? best = null;
        foreach (var (folder, version) in Releases())
        {
            if (version < baseline.Version || (best is not null && version <= best.Version))
            {
                continue;
            }

            if (ReadInstalled(folder) is { } manifest && manifest.DataVersion == version && ShipLoader.IsReadable(manifest.SchemaVersion))
            {
                best = Downloaded(folder, version);
            }
        }

        return best ?? baseline;
    }

    /// <summary>
    /// Unpacks <paramref name="zip"/> into <c>&lt;tag&gt;.tmp\</c>, checks every file against the manifest, writes
    /// <paramref name="manifestJson"/> as it came, and only then renames the folder to <c>&lt;tag&gt;\</c>. Anything wrong
    /// leaves the data as they were: the unfinished folder goes, or, if it won't, an <see cref="IOException"/> names it
    /// and the next start's <see cref="CleanUp"/> takes it.
    /// </summary>
    /// <exception cref="ShipDataFormatException">A schema this app can't read; not a zip; an entry the manifest doesn't list, a listed one missing, a hash that differs; more than the limit unpacked.</exception>
    /// <exception cref="IOException">The download or the disk failed.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission in the data folder.</exception>
    public ActiveShipData Install(ShipDataManifest manifest, ReadOnlySpan<byte> manifestJson, Stream zip)
    {
        if (!ShipLoader.IsReadable(manifest.SchemaVersion))
        {
            throw new ShipDataFormatException($"{manifest.DataVersion}: schemaVersion {manifest.SchemaVersion} needs a newer app.");
        }

        var target = Path.Combine(root, manifest.DataVersion.ToString());
        var temp = target + TempSuffix;
        DeleteIfThere(temp);
        try
        {
            Directory.CreateDirectory(temp);
            Unpack(manifest, zip, temp);
            File.WriteAllBytes(Path.Combine(temp, ManifestFileName), manifestJson);

            // A folder of this tag that Active didn't take (no valid manifest): replaced, never merged.
            DeleteIfThere(target);
            Directory.Move(temp, target);
        }
        catch (Exception ex)
        {
            try
            {
                DeleteIfThere(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The first failure stays the cause; the folder left over is named, and the next start's CleanUp takes it.
                throw new IOException($"{ex.Message} The unfinished folder {temp} stays until the next start: {cleanup.Message}", ex);
            }

            throw;
        }

        return Downloaded(target, manifest.DataVersion);
    }

    /// <summary>
    /// At the start: unfinished installs (<c>*.tmp</c>) and releases older than <paramref name="active"/> go, so one
    /// release stays; newer ones stay too, even of a schema this app can't read (after a downgrade the upgrade needs no
    /// download). Never throws.
    /// </summary>
    /// <returns>One line per folder removed or not removed, for the log.</returns>
    public IReadOnlyList<string> CleanUp(ActiveShipData active)
    {
        List<string> lines = [];
        foreach (var folder in Folders())
        {
            var name = Path.GetFileName(folder);
            var stale = name.EndsWith(TempSuffix, StringComparison.Ordinal)
                || (ShipDataVersion.TryParse(name, out var version) && version < active.Version);
            if (!stale)
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
                lines.Add($"removed {name}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lines.Add($"{name} not removed: {ex.Message}");
            }
        }

        return lines;
    }

    private void Unpack(ShipDataManifest manifest, Stream zip, string temp)
    {
        var asset = manifest.ShipsAsset;
        var inside = Path.GetFullPath(temp) + Path.DirectorySeparatorChar;
        HashSet<string> unpacked = new(StringComparer.Ordinal);
        long total = 0;
        try
        {
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                // A folder entry writes nothing; the files make their folders.
                if (entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                // The manifest's paths are checked (E1), so only listed entries reach the disk; the second test is the net.
                var path = Path.GetFullPath(Path.Combine(temp, entry.FullName));
                if (!manifest.Files.TryGetValue(entry.FullName, out var hash) || !path.StartsWith(inside, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ShipDataFormatException($"{asset} of {manifest.DataVersion} has '{entry.FullName}', which manifest.json doesn't list.");
                }

                if (!unpacked.Add(entry.FullName))
                {
                    throw new ShipDataFormatException($"{asset} of {manifest.DataVersion} has '{entry.FullName}' twice.");
                }

                var data = ReadEntry(entry, maxUnpackedBytes - total, asset);
                total += data.Length;
                if (!Convert.ToHexStringLower(SHA256.HashData(data)).Equals(hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ShipDataFormatException($"'{entry.FullName}' in {asset} of {manifest.DataVersion} doesn't match its hash in manifest.json.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, data);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new ShipDataFormatException($"{asset} of {manifest.DataVersion} is not a zip that can be read: {ex.Message}", ex);
        }

        if (manifest.Files.Keys.Order(StringComparer.Ordinal).FirstOrDefault(p => !unpacked.Contains(p)) is { } missing)
        {
            throw new ShipDataFormatException($"'{missing}' is listed in manifest.json but missing from {asset} of {manifest.DataVersion}.");
        }
    }

    // Read in pieces and counted, not by the entry's own Length: that is what the zip claims.
    private byte[] ReadEntry(ZipArchiveEntry entry, long left, string asset)
    {
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = source.Read(chunk)) > 0)
        {
            if (buffer.Length + read > left)
            {
                throw new ShipDataFormatException($"{asset} unpacks to more than {maxUnpackedBytes} bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private ActiveShipData Downloaded(string folder, ShipDataVersion version) => new(
        version, ShipDataOrigin.Downloaded, folder, Path.Combine(folder, ShipLoader.FolderName), Path.Combine(folder, ShipDataManifestReader.OutlinesPath));

    private IEnumerable<(string Folder, ShipDataVersion Version)> Releases()
    {
        foreach (var folder in Folders())
        {
            if (ShipDataVersion.TryParse(Path.GetFileName(folder), out var version))
            {
                yield return (folder, version);
            }
        }
    }

    // A data folder that can't be listed has no downloads: the baseline runs, as offline.
    private string[] Folders()
    {
        try
        {
            return Directory.Exists(root) ? Directory.GetDirectories(root) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static ShipDataManifest? ReadInstalled(string folder)
    {
        try
        {
            return ShipDataManifestReader.Read(File.ReadAllBytes(Path.Combine(folder, ManifestFileName)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ShipDataFormatException)
        {
            // Not a finished install, or broken since: it doesn't count, and the next install of its tag replaces it.
            return null;
        }
    }

    private static void DeleteIfThere(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
