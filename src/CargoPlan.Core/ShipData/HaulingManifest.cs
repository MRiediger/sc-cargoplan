using System.Security.Cryptography;

namespace CargoPlan.Core.ShipData;

/// <summary>
/// What a data release offers of hauling data (T13.2): the <c>hauling</c> section of its manifest.json
/// (docs/hauling-data-format.md). Never a reason to refuse the release: the hauling data never cost the ship data (K2).
/// </summary>
public abstract record HaulingOffer
{
    private HaulingOffer()
    {
    }

    /// <summary>Both files in a schema this app reads.</summary>
    public sealed record Offered(HaulingManifest Manifest) : HaulingOffer;

    /// <summary><c>hauling</c> missing or <c>null</c>: the release has no hauling data. No notice.</summary>
    public sealed record None : HaulingOffer;

    /// <summary>A schema this app doesn't read, or a section out of form: as an unknown hologram format (D114).</summary>
    public sealed record UnknownFormat(string Reason) : HaulingOffer;
}

/// <summary>The two hauling files of a release, both of <paramref name="SchemaVersion"/>.</summary>
public sealed record HaulingManifest(int SchemaVersion, ReleaseAsset Templates, ReleaseAsset Positions);

/// <summary>A flat asset of a data release: <c>…/releases/download/&lt;tag&gt;/&lt;file&gt;</c>, with its length and hash.</summary>
public sealed record ReleaseAsset(string File, long Bytes, string Sha256)
{
    /// <summary>Whether <paramref name="data"/> is this asset: its length, then its SHA-256.</summary>
    public bool Matches(ReadOnlySpan<byte> data) =>
        data.Length == Bytes && Convert.ToHexStringLower(SHA256.HashData(data)).Equals(Sha256, StringComparison.OrdinalIgnoreCase);
}
