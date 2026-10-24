using System.Buffers.Binary;
using System.Numerics;
using CargoPlan.Core.Holo;

namespace CargoPlan.Core.Tests;

public class HoloReaderTests
{
    private static byte[] Cube()
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        return HoloFixtures.Write(vertices, triangles, HoloFixtures.CubeEdges(), level1: 4, level2: 8, lengthM: 1);
    }

    [Fact]
    public void Reads_the_cube()
    {
        var mesh = HoloReader.Read(Cube());

        mesh.VertexCount.Should().Be(8);
        mesh.TriangleCount.Should().Be(12);
        mesh.EdgeCount.Should().Be(12);
        mesh.EdgeCountFor(1).Should().Be(4);
        mesh.EdgeCountFor(2).Should().Be(8);
        mesh.EdgeCountFor(3).Should().Be(12);
        mesh.LengthM.Should().Be(1);
        mesh.Bounds.Should().Be(new HoloBounds(Vector3.Zero, Vector3.One));
        mesh.Vertex(7).Should().Be(Vector3.One);
        mesh.Triangles.Should().Equal(HoloFixtures.Cube().Triangles);
        mesh.Edges.Should().Equal(HoloFixtures.CubeEdges());
    }

    [Fact]
    public void Reads_32_bit_indices_from_65536_vertices_on()
    {
        Vector3[] vertices = [.. Enumerable.Range(0, 65_536).Select(i => new Vector3(i, i % 2, 0))];
        var data = HoloFixtures.Write(vertices, [0, 1, 65_535], [65_534, 65_535], level1: 1, level2: 1);

        var mesh = HoloReader.Read(data);

        mesh.Triangles.Should().Equal(0, 1, 65_535);
        mesh.Edges.Should().Equal(65_534, 65_535);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Rejects_any_other_length(int extra)
    {
        var data = Cube();
        var wrong = extra < 0 ? data[..^1] : [.. data, 0];

        var read = () => HoloReader.Read(wrong);

        read.Should().Throw<HoloFormatException>().WithMessage("*length*");
    }

    [Fact]
    public void Rejects_a_file_shorter_than_its_header() =>
        ((Action)(() => HoloReader.Read(Cube()[..40]))).Should().Throw<HoloFormatException>();

    [Fact]
    public void Rejects_an_unknown_magic()
    {
        var data = Cube();
        data[3] = (byte)'1';

        ((Action)(() => HoloReader.Read(data))).Should().Throw<HoloFormatException>().WithMessage("*SCH2*");
    }

    [Fact]
    public void Rejects_an_unknown_format_version()
    {
        var data = Cube();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), 3);

        ((Action)(() => HoloReader.Read(data))).Should().Throw<HoloFormatException>().WithMessage("*version 3*");
    }

    [Fact]
    public void Rejects_another_header_size()
    {
        var data = Cube();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), 64);

        ((Action)(() => HoloReader.Read(data))).Should().Throw<HoloFormatException>();
    }

    [Fact]
    public void Rejects_an_index_beyond_the_vertices()
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        triangles[5] = 8;
        var data = HoloFixtures.Write(vertices, triangles, HoloFixtures.CubeEdges(), 4, 8);

        ((Action)(() => HoloReader.Read(data))).Should().Throw<HoloFormatException>().WithMessage("*index 8*");
    }

    [Fact]
    public void Rejects_an_edge_index_beyond_the_vertices()
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        int[] edges = [.. HoloFixtures.CubeEdges()[..^1], 9];
        var data = HoloFixtures.Write(vertices, triangles, edges, 4, 8);

        ((Action)(() => HoloReader.Read(data))).Should().Throw<HoloFormatException>().WithMessage("*index 9*");
    }

    [Theory]
    [InlineData(9, 8)]
    [InlineData(4, 13)]
    public void Rejects_levels_out_of_order(int level1, int level2)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        var data = HoloFixtures.Write(vertices, triangles, HoloFixtures.CubeEdges(), level1, level2);

        ((Action)(() => HoloReader.Read(data))).Should().Throw<HoloFormatException>().WithMessage("*level*");
    }
}
