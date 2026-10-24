using System.Buffers.Binary;
using System.Numerics;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Synthetic holograms in SCH2 (docs/holo-format.md), built here so no game model enters the repo: a cube and a prism,
/// wound outwards, and a writer that lays out the bytes as the pipeline does.
/// </summary>
internal static class HoloFixtures
{
    public const int HeaderBytes = 60;

    /// <summary>The bytes of an SCH2 file; bounds from the vertices, the index type from their count.</summary>
    public static byte[] Write(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, IReadOnlyList<int> edges, int level1, int level2, float lengthM = 10)
    {
        var wide = vertices.Count >= 65_536;
        var indexBytes = wide ? 4 : 2;
        var data = new byte[HeaderBytes + 12 * vertices.Count + indexBytes * (triangles.Count + edges.Count)];
        var span = data.AsSpan();
        "SCH2"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], HeaderBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)vertices.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)(triangles.Count / 3));
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)(edges.Count / 2));
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], (uint)level1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)level2);
        BinaryPrimitives.WriteSingleLittleEndian(span[32..], lengthM);
        var min = vertices.Aggregate(Vector3.Min);
        var max = vertices.Aggregate(Vector3.Max);
        float[] bounds = [min.X, min.Y, min.Z, max.X, max.Y, max.Z];
        for (var i = 0; i < 6; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span[(36 + 4 * i)..], bounds[i]);
        }

        var at = HeaderBytes;
        foreach (var v in vertices)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span[at..], v.X);
            BinaryPrimitives.WriteSingleLittleEndian(span[(at + 4)..], v.Y);
            BinaryPrimitives.WriteSingleLittleEndian(span[(at + 8)..], v.Z);
            at += 12;
        }

        foreach (var index in triangles.Concat(edges))
        {
            if (wide)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span[at..], (uint)index);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(span[at..], (ushort)index);
            }

            at += indexBytes;
        }

        return data;
    }

    /// <summary>The unit cube from 0 to 1, vertex i at (i &amp; 1, i &gt;&gt; 1 &amp; 1, i &gt;&gt; 2 &amp; 1), two triangles per face.</summary>
    public static (Vector3[] Vertices, int[] Triangles) Cube()
    {
        Vector3[] vertices = [.. Enumerable.Range(0, 8).Select(i => new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1))];
        int[][] faces = [[0, 1, 3, 2], [4, 5, 7, 6], [0, 1, 5, 4], [2, 3, 7, 6], [0, 2, 6, 4], [1, 3, 7, 5]];
        return (vertices, [.. faces.SelectMany(f => Quad(vertices, f, new Vector3(0.5f)))]);
    }

    /// <summary>The cube's 12 edges, as a feature edge list ordered into levels by the caller.</summary>
    public static int[] CubeEdges() =>
        [.. from i in Enumerable.Range(0, 8) from bit in (int[])[1, 2, 4] where (i & bit) == 0 from v in new[] { i, i | bit } select v];

    /// <summary>
    /// A prism with a regular <paramref name="sides"/>-gon of radius 1 around z, from z = 0 to 1: bottom ring 0..n−1,
    /// top ring n..2n−1, vertex k at angle k · 360° / n; caps as fans from vertex 0 and n.
    /// </summary>
    public static (Vector3[] Vertices, int[] Triangles) Prism(int sides)
    {
        Vector3[] vertices =
        [
            .. from z in new[] { 0f, 1f }
               from k in Enumerable.Range(0, sides)
               let a = 2 * Math.PI * k / sides
               select new Vector3((float)Math.Cos(a), (float)Math.Sin(a), z),
        ];
        var centre = new Vector3(0, 0, 0.5f);
        List<int> triangles = [];
        for (var k = 0; k < sides; k++)
        {
            var next = (k + 1) % sides;
            triangles.AddRange(Quad(vertices, [k, next, sides + next, sides + k], centre));
        }

        for (var k = 1; k < sides - 1; k++)
        {
            triangles.AddRange(Outward(vertices, [0, k, k + 1], centre));
            triangles.AddRange(Outward(vertices, [sides, sides + k, sides + k + 1], centre));
        }

        return (vertices, [.. triangles]);
    }

    private static int[] Quad(Vector3[] v, int[] q, Vector3 centre) =>
        [.. Outward(v, [q[0], q[1], q[2]], centre), .. Outward(v, [q[0], q[2], q[3]], centre)];

    // Counter-clockwise seen from outside: the normal points away from the centre of the convex body.
    private static int[] Outward(Vector3[] v, int[] t, Vector3 centre)
    {
        var normal = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
        var middle = (v[t[0]] + v[t[1]] + v[t[2]]) / 3;
        return Vector3.Dot(normal, middle - centre) >= 0 ? t : [t[0], t[2], t[1]];
    }
}
