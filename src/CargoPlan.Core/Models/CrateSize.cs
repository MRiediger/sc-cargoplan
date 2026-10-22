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
        /// <summary>Crate shape in cells, in its only orientation (crates are never turned).</summary>
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
    }
}
