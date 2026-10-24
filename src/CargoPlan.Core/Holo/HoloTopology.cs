using System.Numerics;

namespace CargoPlan.Core.Holo;

/// <summary>Edges and normals of a <see cref="HoloMesh"/>, shared by <see cref="HoloSurface"/> and <see cref="HoloContour"/>.</summary>
internal static class HoloTopology
{
    /// <summary>An undirected edge as one number: the smaller vertex index in the high half.</summary>
    public static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    public static (int A, int B) Ends(long key) => ((int)(key >> 32), (int)(key & uint.MaxValue));

    /// <summary>
    /// For sets and dictionaries of <see cref="Key"/>s. long's own hash is low XOR high, here a ^ b: neighbouring
    /// vertices give the same few values, and the Centaurus took 2 s instead of milliseconds. Fibonacci hashing mixes
    /// all 64 bits first.
    /// </summary>
    public static IEqualityComparer<long> KeyComparer { get; } = new MixedKeyComparer();

    /// <summary>Every edge of the triangles with the first two triangles on it and how many there are in all.</summary>
    public static Dictionary<long, SharedBy> TrianglesByEdge(HoloMesh mesh)
    {
        var t = mesh.Triangles;
        Dictionary<long, SharedBy> edges = new(t.Length, KeyComparer);
        for (var i = 0; i < mesh.TriangleCount; i++)
        {
            for (var k = 0; k < 3; k++)
            {
                var key = Key(t[3 * i + k], t[3 * i + (k + 1) % 3]);
                edges[key] = edges.TryGetValue(key, out var shared) ? shared with { Second = shared.Count == 1 ? i : shared.Second, Count = shared.Count + 1 } : new SharedBy(i, -1, 1);
            }
        }

        return edges;
    }

    /// <summary>Unit length, outwards by the winding; zero for a triangle without area.</summary>
    public static Vector3 FaceNormal(HoloMesh mesh, int triangle)
    {
        var t = mesh.Triangles;
        var (a, b, c) = (mesh.Vertex(t[3 * triangle]), mesh.Vertex(t[3 * triangle + 1]), mesh.Vertex(t[3 * triangle + 2]));
        var cross = Vector3.Cross(b - a, c - a);
        return cross == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(cross);
    }

    /// <summary>The triangle's inner angle in radians at its corner <paramref name="corner"/> (3 × triangle + k).</summary>
    public static float CornerAngle(HoloMesh mesh, int corner)
    {
        var t = mesh.Triangles;
        var start = corner - corner % 3;
        var at = mesh.Vertex(t[corner]);
        var u = mesh.Vertex(t[start + (corner - start + 1) % 3]) - at;
        var w = mesh.Vertex(t[start + (corner - start + 2) % 3]) - at;
        return MathF.Atan2(Vector3.Cross(u, w).Length(), Vector3.Dot(u, w));
    }

    /// <param name="Second">-1 while only one triangle has the edge.</param>
    public readonly record struct SharedBy(int First, int Second, int Count);

    private sealed class MixedKeyComparer : IEqualityComparer<long>
    {
        // 2^64 / golden ratio.
        private const ulong Fibonacci = 0x9E3779B97F4A7C15;

        public bool Equals(long x, long y) => x == y;

        public int GetHashCode(long key) => (int)(((ulong)key * Fibonacci) >> 32);
    }
}
