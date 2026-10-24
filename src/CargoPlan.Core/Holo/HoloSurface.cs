using System.Numerics;

namespace CargoPlan.Core.Holo;

/// <summary>
/// A hologram's triangles as a renderer takes them, one per look (D63): positions in metres, a normal per vertex (empty
/// for <see cref="Opaque"/>), three indices per triangle. WPF-free, so the app prepares it off the UI thread.
/// </summary>
public sealed record HoloSurface(float[] Positions, float[] Normals, int[] Indices)
{
    /// <summary>The flat look: every triangle its own three vertices with its face normal, so no shading crosses an edge.</summary>
    public static HoloSurface Flat(HoloMesh mesh)
    {
        var t = mesh.Triangles;
        var positions = new float[3 * t.Length];
        var normals = new float[3 * t.Length];
        for (var c = 0; c < t.Length; c++)
        {
            Put(positions, c, mesh.Vertex(t[c]));
            Put(normals, c, HoloTopology.FaceNormal(mesh, c / 3));
        }

        return new HoloSurface(positions, normals, [.. Enumerable.Range(0, t.Length)]);
    }

    /// <summary>
    /// The shaded look: smooth across the hull, sharp at its creases. A vertex is split where feature edges (all levels)
    /// separate the triangles around it; each copy's normal is the mean of its triangles' normals, weighted by their
    /// angle at the vertex, so it doesn't depend on how a face is cut into triangles.
    /// </summary>
    public static HoloSurface Smooth(HoloMesh mesh)
    {
        var t = mesh.Triangles;

        // Union-find over the corners (3 × triangle + k): two triangles' corners at the same vertex join across an edge
        // they share, unless it is a feature edge or not shared by exactly these two.
        var parent = new int[t.Length];
        for (var c = 0; c < parent.Length; c++)
        {
            parent[c] = c;
        }

        HashSet<long> features = new(Enumerable.Range(0, mesh.EdgeCount).Select(e => HoloTopology.Key(mesh.Edges[2 * e], mesh.Edges[2 * e + 1])), HoloTopology.KeyComparer);
        foreach (var (key, shared) in HoloTopology.TrianglesByEdge(mesh))
        {
            if (shared.Count != 2 || features.Contains(key))
            {
                continue;
            }

            var (a, b) = HoloTopology.Ends(key);
            Union(parent, Corner(t, shared.First, a), Corner(t, shared.Second, a));
            Union(parent, Corner(t, shared.First, b), Corner(t, shared.Second, b));
        }

        Dictionary<int, int> vertexOf = [];
        var indices = new int[t.Length];
        List<Vector3> positions = [];
        List<Vector3> sums = [];
        for (var c = 0; c < t.Length; c++)
        {
            var root = Find(parent, c);
            if (!vertexOf.TryGetValue(root, out var v))
            {
                v = vertexOf[root] = positions.Count;
                positions.Add(mesh.Vertex(t[c]));
                sums.Add(Vector3.Zero);
            }

            indices[c] = v;
            sums[v] += HoloTopology.CornerAngle(mesh, c) * HoloTopology.FaceNormal(mesh, c / 3);
        }

        var flatPositions = new float[3 * positions.Count];
        var normals = new float[3 * positions.Count];
        for (var v = 0; v < positions.Count; v++)
        {
            Put(flatPositions, v, positions[v]);
            Put(normals, v, sums[v] == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(sums[v]));
        }

        return new HoloSurface(flatPositions, normals, indices);
    }

    /// <summary>The hidden-line look's fill: the welded mesh as stored, drawn in one colour without light, so no normals.</summary>
    public static HoloSurface Opaque(HoloMesh mesh) => new(mesh.Vertices, [], mesh.Triangles);

    private static int Corner(int[] t, int triangle, int vertex) =>
        t[3 * triangle] == vertex ? 3 * triangle : t[3 * triangle + 1] == vertex ? 3 * triangle + 1 : 3 * triangle + 2;

    private static int Find(int[] parent, int c)
    {
        while (parent[c] != c)
        {
            parent[c] = parent[parent[c]];
            c = parent[c];
        }

        return c;
    }

    private static void Union(int[] parent, int a, int b) => parent[Find(parent, a)] = Find(parent, b);

    private static void Put(float[] array, int index, Vector3 value)
    {
        array[3 * index] = value.X;
        array[3 * index + 1] = value.Y;
        array[3 * index + 2] = value.Z;
    }
}
