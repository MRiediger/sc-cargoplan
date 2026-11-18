using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A data release as <c>sc-cargoplan-shipdata</c> publishes it (its docs/manifest-format.md), built from the frozen ships
/// and outlines (A10): <c>ships.zip</c> with <c>ships/**</c> and <c>outlines.json</c>, and a manifest whose <c>files</c>
/// hash every entry. The App tests link this file.
/// </summary>
internal sealed class ShipDataRelease
{
    /// <summary>Path inside the zip → content; change it before <see cref="Zip"/> to break the release.</summary>
    public SortedDictionary<string, byte[]> Entries { get; } = new(StringComparer.Ordinal);

    public string DataVersion { get; init; } = "ships-2026.11.1";

    public int SchemaVersion { get; init; } = 6;

    /// <summary>The frozen ships, their module and the frozen outlines.</summary>
    public static ShipDataRelease Frozen(string dataVersion = "ships-2026.11.1", int schemaVersion = 6)
    {
        var release = new ShipDataRelease { DataVersion = dataVersion, SchemaVersion = schemaVersion };
        foreach (var file in Directory.EnumerateFiles(FrozenShips.Folder, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(FrozenShips.Folder, file).Replace('\\', '/');
            release.Entries["ships/" + relative] = File.ReadAllBytes(file);
        }

        release.Entries["outlines.json"] = File.ReadAllBytes(FrozenOutlines.PathOfFile);
        return release;
    }

    /// <summary>
    /// The manifest of the entries as they are now; <paramref name="files"/> overrides the hashes (null = from the entries).
    /// Shaped like the published one, with fields the app doesn't read.
    /// </summary>
    public byte[] Manifest(IReadOnlyDictionary<string, string>? files = null, Action<JsonObject>? change = null)
    {
        var hashes = new JsonObject();
        foreach (var (path, hash) in files ?? Entries.ToDictionary(e => e.Key, e => Sha256(e.Value)))
        {
            hashes[path] = hash;
        }

        var manifest = new JsonObject
        {
            ["dataVersion"] = DataVersion,
            ["schemaVersion"] = SchemaVersion,
            ["generatedAt"] = "2026-11-18T04:23:00Z",
            ["builtFrom"] = new JsonObject { ["appTag"] = "v0.5.1", ["pipelineCommit"] = "c823a5e" },
            ["assets"] = new JsonObject { ["ships"] = "ships.zip" },
            ["files"] = hashes,
            ["holo"] = new JsonObject { ["formatVersion"] = 2, ["ships"] = new JsonObject() },
            ["ships"] = new JsonObject
            {
                ["hull_a"] = new JsonObject { ["modelKey"] = "misc-hull-a", ["missingSince"] = null, ["follows"] = null },
            },
        };
        change?.Invoke(manifest);
        return Encoding.UTF8.GetBytes(manifest.ToJsonString());
    }

    /// <summary>The zip of the entries; <paramref name="extra"/> adds entries the manifest doesn't know.</summary>
    public byte[] Zip(params (string Path, byte[] Data)[] extra)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, data) in Entries.Select(e => (e.Key, e.Value)).Concat(extra))
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(data);
            }
        }

        return buffer.ToArray();
    }

    public static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}

/// <summary>
/// A stream that fails once <paramref name="limit"/> bytes were read in all: a download or disk that breaks off mid-way.
/// Counted over every read, not by position: a zip is read from its directory at the end first.
/// </summary>
internal sealed class BreakingStream(byte[] data, int limit) : Stream
{
    private readonly MemoryStream _inner = new(data);

    private long _read;

    /// <summary>Whether the stream broke: the reader got this far.</summary>
    public bool Broke { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _inner.Length;

    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_read >= limit)
        {
            Broke = true;
            throw new IOException("The connection broke off (test).");
        }

        var read = _inner.Read(buffer, offset, (int)Math.Min(count, limit - _read));
        _read += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
