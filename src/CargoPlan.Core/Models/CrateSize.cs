namespace CargoPlan.Core.Models;

/// <summary>Physical crate size. The numeric value is the crate's volume in SCU.</summary>
public enum CrateSize
{
    One = 1,
    Two = 2,
    Four = 4,
    Eight = 8,
    Sixteen = 16,
    TwentyFour = 24,
    ThirtyTwo = 32,
}

// C# 14 extension members: https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/extension
public static class CrateSizeExtensions
{
    /// <param name="size">The crate size used as a receiver.</param>
    extension(CrateSize size)
    {
        /// <summary>
        /// Crate shape in cells as it lies unturned: length along the rack's length. <see cref="Shape"/> gives it turned (D71).
        /// </summary>
        public Dimensions Dimensions => size switch
        {
            CrateSize.One => new(1, 1, 1),
            CrateSize.Two => new(2, 1, 1),
            CrateSize.Four => new(2, 2, 1),
            CrateSize.Eight => new(2, 2, 2),
            CrateSize.Sixteen => new(4, 2, 2),
            CrateSize.TwentyFour => new(6, 2, 2),
            // 10 × 2.5 × 2.5 m: Star Citizen Wiki and Fleetyards' maxContainerSize (D57).
            CrateSize.ThirtyTwo => new(8, 2, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown crate size."),
        };

        /// <summary>True if the footprint is oblong (2, 16, 24, 32): only such a crate lies another way turned (D71).</summary>
        public bool Turns => size.Dimensions.Length != size.Dimensions.Width;

        /// <summary>
        /// The shape in the grid, turned 90° about the stack axis or not (D71): length and width swap, the stack stays.
        /// The plane they lie in is the rack's, across its <c>StackAxis</c>; on the Hull A's upright plates it stands upright.
        /// </summary>
        public Dimensions Shape(bool rotated) =>
            rotated ? new(size.Dimensions.Width, size.Dimensions.Length, size.Dimensions.Stack) : size.Dimensions;
    }
}
