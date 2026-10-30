using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

/// <summary>The 3D scene's models (T8.2).</summary>
public class ShipScene3DTests
{
    private static readonly ShipBox Box = new(new ShipPoint(0, 0, 0), new ShipPoint(4, 2, 2));

    [Fact]
    public void A_crate_carries_texture_coordinates_in_cells_on_every_face()
    {
        var mesh = ShipScene3D.Cuboid(Box);

        mesh.TextureCoordinates.Should().HaveCount(mesh.Positions.Count);
        for (var face = 0; face < 6; face++)
        {
            // Along each edge the texture moves by the edge's length: one unit per cell.
            for (var corner = 0; corner < 4; corner++)
            {
                var (i, j) = (4 * face + corner, 4 * face + (corner + 1) % 4);
                ((mesh.TextureCoordinates[j] - mesh.TextureCoordinates[i]).Length)
                    .Should().BeApproximately((mesh.Positions[j] - mesh.Positions[i]).Length, 1e-9);
            }
        }
    }

    [Fact]
    public void No_face_shows_its_hatch_mirrored_seen_from_outside()
    {
        // u runs right and v down as the face is seen from outside, so u × v points into the face on every face: a
        // forward hatch stays forward on port and starboard alike, and the second round stays apart from the third.
        var mesh = ShipScene3D.Cuboid(Box);
        for (var face = 0; face < 6; face++)
        {
            var (a, b, d) = (4 * face, 4 * face + 1, 4 * face + 3);
            var (uv0, uv1, uv3) = (mesh.TextureCoordinates[a], mesh.TextureCoordinates[b], mesh.TextureCoordinates[d]);
            var (e1, e2) = (mesh.Positions[b] - mesh.Positions[a], mesh.Positions[d] - mesh.Positions[a]);
            var (t1, t2) = (uv1 - uv0, uv3 - uv0);

            // Solve e = du·U + dv·V for the 3D directions of u and v on this face.
            var det = t1.X * t2.Y - t2.X * t1.Y;
            var u = (t2.Y * e1 - t1.Y * e2) / det;
            var v = (-t2.X * e1 + t1.X * e2) / det;

            Vector3D.DotProduct(Vector3D.CrossProduct(u, v), mesh.Normals[a]).Should().BeNegative($"face {face}");
        }
    }
}
