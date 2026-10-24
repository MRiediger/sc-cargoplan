using System.Buffers.Binary;
using System.Numerics;
using static System.FormattableString;

namespace CargoPlan.Core.Holo;

/// <summary>
/// Reads an SCH2 file (docs/holo-format.md): little endian, no compression, the length fixed by the header. The file
/// boundary of the holograms: every rule of the format is checked here, so the drawing can trust the mesh (A6).
/// </summary>
public static class HoloReader
{
    public const int FormatVersion = 2;

    private const int HeaderBytes = 60;

    // From this many vertices on, indices are uint32 instead of uint16.
    private const int WideIndexVertices = 65_536;

    /// <exception cref="HoloFormatException">Not SCH2, another version or header, the wrong length, an index beyond the vertices, or levels out of order.</exception>
    public static HoloMesh Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderBytes)
        {
            throw new HoloFormatException(Invariant($"{data.Length} bytes is shorter than the header."));
        }

        if (!data[..4].SequenceEqual("SCH2"u8))
        {
            throw new HoloFormatException("not an SCH2 file.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        if (version != FormatVersion)
        {
            throw new HoloFormatException(Invariant($"format version {version} is not supported (only {FormatVersion})."));
        }

        var headerBytes = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        if (headerBytes != HeaderBytes)
        {
            throw new HoloFormatException(Invariant($"header of {headerBytes} bytes, version {FormatVersion} has {HeaderBytes}."));
        }

        long vertexCount = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
        long triangleCount = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]);
        long edgeCount = BinaryPrimitives.ReadUInt32LittleEndian(data[20..]);
        long level1 = BinaryPrimitives.ReadUInt32LittleEndian(data[24..]);
        long level2 = BinaryPrimitives.ReadUInt32LittleEndian(data[28..]);
        if (level1 > level2 || level2 > edgeCount)
        {
            throw new HoloFormatException(Invariant($"edge levels {level1} / {level2} / {edgeCount} are not in order."));
        }

        // In long: counts up to 2^32 must not overflow before the comparison with the real length.
        var indexBytes = vertexCount < WideIndexVertices ? 2 : 4;
        var expected = HeaderBytes + 12 * vertexCount + indexBytes * (3 * triangleCount + 2 * edgeCount);
        if (data.Length != expected)
        {
            throw new HoloFormatException(Invariant($"length {data.Length} bytes, the header says {expected}."));
        }

        var lengthM = BinaryPrimitives.ReadSingleLittleEndian(data[32..]);
        var bounds = new HoloBounds(ReadVector(data[36..]), ReadVector(data[48..]));

        var vertices = new float[3 * vertexCount];
        var at = HeaderBytes;
        for (var i = 0; i < vertices.Length; i++, at += 4)
        {
            vertices[i] = BinaryPrimitives.ReadSingleLittleEndian(data[at..]);
        }

        var triangles = ReadIndices(data, ref at, (int)(3 * triangleCount), indexBytes, (int)vertexCount);
        var edges = ReadIndices(data, ref at, (int)(2 * edgeCount), indexBytes, (int)vertexCount);
        return new HoloMesh(lengthM, bounds, vertices, triangles, edges, (int)level1, (int)level2);
    }

    private static Vector3 ReadVector(ReadOnlySpan<byte> data) => new(
        BinaryPrimitives.ReadSingleLittleEndian(data),
        BinaryPrimitives.ReadSingleLittleEndian(data[4..]),
        BinaryPrimitives.ReadSingleLittleEndian(data[8..]));

    private static int[] ReadIndices(ReadOnlySpan<byte> data, ref int at, int count, int indexBytes, int vertexCount)
    {
        var indices = new int[count];
        for (var i = 0; i < count; i++, at += indexBytes)
        {
            long index = indexBytes == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data[at..]) : BinaryPrimitives.ReadUInt32LittleEndian(data[at..]);
            if (index >= vertexCount)
            {
                throw new HoloFormatException(Invariant($"index {index} at byte {at}, only {vertexCount} vertices."));
            }

            indices[i] = (int)index;
        }

        return indices;
    }
}
