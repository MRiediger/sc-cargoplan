using System.Numerics;
using CargoPlan.Core.Holo;

namespace CargoPlan.Core.Tests;

public class HoloGeometryTests
{
    private static HoloMesh CubeMesh(bool withEdges)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        var edges = withEdges ? HoloFixtures.CubeEdges() : [];
        return HoloReader.Read(HoloFixtures.Write(vertices, triangles, edges, edges.Length / 2, edges.Length / 2));
    }

    private static HoloMesh PrismMesh(int sides)
    {
        var (vertices, triangles) = HoloFixtures.Prism(sides);
        return HoloReader.Read(HoloFixtures.Write(vertices, triangles, [], 0, 0));
    }

    // --- Surfaces ---

    [Fact]
    public void Flat_splits_every_triangle_with_its_face_normal()
    {
        var surface = HoloSurface.Flat(CubeMesh(withEdges: true));

        surface.Positions.Length.Should().Be(36 * 3);
        surface.Indices.Should().Equal(Enumerable.Range(0, 36));
        var normals = Normals(surface);
        normals.Distinct().Should().BeEquivalentTo([Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ]);
    }

    [Fact]
    public void Smooth_splits_vertices_along_the_feature_edges()
    {
        var surface = HoloSurface.Smooth(CubeMesh(withEdges: true));

        // Every face keeps its own four corners: 6 × 4, each with the face's normal.
        surface.Positions.Length.Should().Be(24 * 3);
        surface.Indices.Length.Should().Be(36);
        Normals(surface).Should().OnlyContain(n => Math.Abs(n.Length() - 1) < 1e-6 && (Math.Abs(n.X) == 1 || Math.Abs(n.Y) == 1 || Math.Abs(n.Z) == 1));
    }

    [Fact]
    public void Smooth_keeps_welded_vertices_where_no_edge_separates_them()
    {
        var surface = HoloSurface.Smooth(CubeMesh(withEdges: false));

        surface.Positions.Length.Should().Be(8 * 3);

        // Weighted by the angle at the corner, not by area: each of the three faces counts 90°, however it is split into
        // triangles, so the corner's normal points out along the diagonal.
        var diagonal = 1 / MathF.Sqrt(3);
        Normals(surface).Should().OnlyContain(n =>
            Math.Abs(Math.Abs(n.X) - diagonal) < 1e-6 && Math.Abs(Math.Abs(n.Y) - diagonal) < 1e-6 && Math.Abs(Math.Abs(n.Z) - diagonal) < 1e-6);
    }

    [Fact]
    public void Opaque_is_the_welded_mesh_without_normals()
    {
        var mesh = CubeMesh(withEdges: true);

        var surface = HoloSurface.Opaque(mesh);

        surface.Positions.Should().Equal(mesh.Vertices);
        surface.Indices.Should().Equal(mesh.Triangles);
        surface.Normals.Should().BeEmpty();
    }

    // --- Edge levels by zoom ---

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(1, 1)]
    [InlineData(1.49, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.99, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 3)]
    public void The_edge_level_follows_the_zoom(double zoom, int level) => HoloEdges.LevelFor(zoom).Should().Be(level);

    // --- Contour ---

    [Fact]
    public void A_cube_has_no_contour_candidates_its_creases_are_feature_edges()
    {
        var contour = HoloContour.Build(CubeMesh(withEdges: true));

        contour.Visible(new Vector3(-1, 0, 0)).Should().BeEmpty();
        contour.Visible(Vector3.Normalize(new Vector3(-1, -1, -1))).Should().BeEmpty();
    }

    [Fact]
    public void A_prism_seen_from_the_side_has_its_two_silhouette_edges_as_contour()
    {
        // 16 sides meet at 22.5°, under the 30° of the format, so they are contour candidates; caps meet sides at 90°.
        var contour = HoloContour.Build(PrismMesh(16));

        // Looking along −x: the faces with an outward normal towards +x face the camera; they turn at 90° and 270°.
        var edges = contour.Visible(new Vector3(-1, 0, 0));

        Pairs(edges).Should().BeEquivalentTo([(4, 20), (12, 28)]);
    }

    [Fact]
    public void A_prism_seen_from_above_has_no_contour_on_its_sides()
    {
        var contour = HoloContour.Build(PrismMesh(16));

        contour.Visible(new Vector3(0, 0, -1)).Should().BeEmpty();
    }

    private static Vector3[] Normals(HoloSurface surface) =>
        [.. Enumerable.Range(0, surface.Normals.Length / 3).Select(i => new Vector3(surface.Normals[3 * i], surface.Normals[3 * i + 1], surface.Normals[3 * i + 2]))];

    private static (int, int)[] Pairs(int[] edges) =>
        [.. Enumerable.Range(0, edges.Length / 2).Select(i => (Math.Min(edges[2 * i], edges[2 * i + 1]), Math.Max(edges[2 * i], edges[2 * i + 1])))];
}
