using System.Numerics;

namespace CargoPlan.Core.Holo;

/// <summary>Which edge level the hidden-line look draws at a zoom (D63): more detail the closer the player zooms in.</summary>
public static class HoloEdges
{
    /// <summary>From this zoom on level 2 …</summary>
    public const double Level2Zoom = 1.5;

    /// <summary>… and from this one level 3, all edges.</summary>
    public const double Level3Zoom = 3;

    /// <param name="zoom">The ship view's zoom on top of the fit; 1 is the default fit.</param>
    public static int LevelFor(double zoom) => zoom >= Level3Zoom ? 3 : zoom >= Level2Zoom ? 2 : 1;
}

/// <summary>
/// The hidden-line contour (docs/holo-format.md): edges between a triangle facing the camera and one facing away, only
/// where the two meet at less than 30°. The candidates are found once per mesh; <see cref="Visible"/> runs per camera.
/// </summary>
public sealed class HoloContour
{
    /// <summary>Part of format version 2: creases from here on are feature edges, never contour.</summary>
    public const double MaxCreaseDegrees = 30;

    private readonly int[] _edges;
    private readonly Vector3[] _normals;

    private HoloContour(int[] edges, Vector3[] normals)
    {
        _edges = edges;
        _normals = normals;
    }

    /// <summary>Every edge of exactly two triangles meeting at less than <see cref="MaxCreaseDegrees"/>.</summary>
    public static HoloContour Build(HoloMesh mesh)
    {
        var minCos = (float)Math.Cos(MaxCreaseDegrees * Math.PI / 180);
        List<int> edges = [];
        List<Vector3> normals = [];
        foreach (var (key, shared) in HoloTopology.TrianglesByEdge(mesh))
        {
            if (shared.Count != 2)
            {
                continue;
            }

            var (n1, n2) = (HoloTopology.FaceNormal(mesh, shared.First), HoloTopology.FaceNormal(mesh, shared.Second));
            if (n1 == Vector3.Zero || n2 == Vector3.Zero || Vector3.Dot(n1, n2) <= minCos)
            {
                continue;
            }

            var (a, b) = HoloTopology.Ends(key);
            edges.AddRange([a, b]);
            normals.AddRange([n1, n2]);
        }

        return new HoloContour([.. edges], [.. normals]);
    }

    /// <summary>How many candidates there are; the most <see cref="Visible"/> can return.</summary>
    public int CandidateCount => _normals.Length / 2;

    /// <summary>The contour seen along <paramref name="viewDirection"/>, as vertex index pairs; a triangle faces the camera when its normal points against the view.</summary>
    public int[] Visible(Vector3 viewDirection)
    {
        List<int> visible = [];
        for (var i = 0; i < CandidateCount; i++)
        {
            if (Vector3.Dot(_normals[2 * i], viewDirection) < 0 != Vector3.Dot(_normals[2 * i + 1], viewDirection) < 0)
            {
                visible.Add(_edges[2 * i]);
                visible.Add(_edges[2 * i + 1]);
            }
        }

        return [.. visible];
    }
}
