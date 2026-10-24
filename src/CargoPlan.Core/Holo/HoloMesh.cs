using System.Numerics;

namespace CargoPlan.Core.Holo;

/// <summary>
/// A ship hologram as SCH2 stores it (docs/holo-format.md, D63): metres in ship axes (x bow, y port, z up), origin in
/// the middle of the x/y box with z = 0 at the keel; welded triangles wound outwards; feature edges in three levels,
/// each level the first edges of the list. The arrays are read once and never changed.
/// </summary>
/// <param name="Vertices">x, y, z per vertex.</param>
/// <param name="Triangles">Three vertex indices per triangle.</param>
/// <param name="Edges">Two vertex indices per edge: level 1 first, then the rest of level 2, then the rest of level 3.</param>
public sealed record HoloMesh(float LengthM, HoloBounds Bounds, float[] Vertices, int[] Triangles, int[] Edges, int Level1Count, int Level2Count)
{
    public int VertexCount => Vertices.Length / 3;

    public int TriangleCount => Triangles.Length / 3;

    public int EdgeCount => Edges.Length / 2;

    /// <summary>The edges level <paramref name="level"/> draws (1–3): the first this many of <see cref="Edges"/>.</summary>
    public int EdgeCountFor(int level) => level switch
    {
        1 => Level1Count,
        2 => Level2Count,
        3 => EdgeCount,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Levels are 1 to 3."),
    };

    public Vector3 Vertex(int index) => new(Vertices[3 * index], Vertices[3 * index + 1], Vertices[3 * index + 2]);
}

/// <summary>The vertices' box in metres, from the file's header.</summary>
public readonly record struct HoloBounds(Vector3 Min, Vector3 Max);
