namespace CargoPlan.Core.Holo;

/// <summary>
/// What a ship data release offers this app of holograms (T12.4, D114): the <c>holo</c> section of its manifest.json,
/// read by <see cref="ShipData.ShipDataManifestReader"/>.
/// </summary>
public abstract record HoloOffer
{
    private HoloOffer()
    {
    }

    /// <summary>Holograms in the format this app reads (<see cref="HoloReader.FormatVersion"/>).</summary>
    public sealed record Offered(HoloManifest Manifest) : HoloOffer;

    /// <summary><c>holo: null</c>, the kill switch (D63): the release offers none, every ship shows its outline.</summary>
    public sealed record NotOffered : HoloOffer;

    /// <summary>Holograms in a format this app doesn't read (R1 of T12.4): none for this app, the ships still load.</summary>
    public sealed record UnknownFormat(int FormatVersion) : HoloOffer;
}

/// <summary>Every hologram of a data release. A ship that isn't listed has none; the app shows its outline (D62).</summary>
/// <param name="DataVersion">The release tag: the holograms are its assets, and the cache is named after it.</param>
/// <param name="Ships">Sorted by slug.</param>
public sealed record HoloManifest(string DataVersion, IReadOnlyList<HoloManifestEntry> Ships)
{
    /// <summary>The entry of <paramref name="slug"/> (a ship's <c>modelKey</c>, case-sensitive), or null.</summary>
    public HoloManifestEntry? Find(string slug) => Ships.FirstOrDefault(s => s.Slug == slug);

    // By content, not by the list's reference: the same manifest read twice is the same data (ActiveShipData compares).
    public bool Equals(HoloManifest? other) =>
        other is not null && DataVersion == other.DataVersion && Ships.SequenceEqual(other.Ships);

    public override int GetHashCode() => HashCode.Combine(DataVersion, Ships.Count);
}

/// <param name="File">The asset's name in the release, flat.</param>
/// <param name="Bytes">The file's length; a download of another length is broken.</param>
/// <param name="Sha256">Hex; a cached file with another hash is stale.</param>
public sealed record HoloManifestEntry(string Slug, string File, long Bytes, string Sha256);
