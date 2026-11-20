using CargoPlan.Core.Models;

namespace CargoPlan.Core.Planning;

/// <summary>
/// A rack's width split into lanes (D116): the first side, the second side and, on an odd width, the middle column. Each
/// lane fills like a whole rack (<see cref="CrateLayout.Ranks"/>), and no crate lies across two of them.
/// </summary>
internal sealed class Lanes
{
    /// <summary>The narrowest rack that gets lanes: two cells a side, so every crate fits a lane unturned (D57).</summary>
    public const int MinWidth = 4;

    private readonly int[] _laneOf;
    private readonly int[][] _breadth;

    private Lanes(int[] laneOf, int[][] breadth) => (_laneOf, _breadth) = (laneOf, breadth);

    /// <summary>The lanes in fill order.</summary>
    public int Count => _breadth.Length;

    /// <summary>The lane a width index belongs to.</summary>
    public int LaneOf(int width) => _laneOf[width];

    /// <summary>A lane's width indices, from its outer edge towards the middle.</summary>
    public IReadOnlyList<int> Breadth(int lane) => _breadth[lane];

    /// <summary>True if <paramref name="span"/> cells from <paramref name="width"/> on lie in two lanes.</summary>
    public bool Splits(int width, int span) => _laneOf[width] != _laneOf[width + span - 1];

    /// <summary>
    /// The lanes of <paramref name="rack"/> under <paramref name="sides"/>; null without sides, on a rack stacking along
    /// ±y (its width stands upright, no side to it) and on one under <see cref="MinWidth"/> cells wide. Width 0 is
    /// starboard wherever width lies across the ship (<see cref="Rack.FrameOf"/>, D116): the first lane is the port half
    /// for <see cref="Sides.PortFirst"/>, each lane runs from its outer edge, an odd width's middle column is the last.
    /// </summary>
    public static Lanes? Of(Rack rack, Sides sides)
    {
        var width = rack.Grid.Width;
        if (sides == Sides.None || rack.StacksSideways || width < MinWidth)
        {
            return null;
        }

        var half = width / 2;
        int[] port = [.. Enumerable.Range(width - half, half).Reverse()];
        int[] starboard = [.. Enumerable.Range(0, half)];
        int[][] breadth = sides == Sides.PortFirst ? [port, starboard] : [starboard, port];
        if (width % 2 == 1)
        {
            breadth = [.. breadth, [half]];
        }

        var laneOf = new int[width];
        for (var lane = 0; lane < breadth.Length; lane++)
        {
            foreach (var w in breadth[lane])
            {
                laneOf[w] = lane;
            }
        }

        return new Lanes(laneOf, breadth);
    }
}
