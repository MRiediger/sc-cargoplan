using CargoPlan.Import.Contracts;

namespace CargoPlan.Import.Tests;

/// <summary>
/// A pickup place as the parser takes it (B6, D69): a list line or a heading the OCR ran into is cut off, a dash inside a
/// name stays. Held by PickupNames' tests until T10.4a; the parser's Collect lines use the same rule.
/// </summary>
public class ContractTextTests
{
    [Theory]
    [InlineData("- Freight elevator at Green Glade Station", "")]
    [InlineData("DROP OFF LOCATIONS (ANY ORDER)", "")]
    [InlineData("Everus Harbor. - Freight elevator at Green Glade Station at Hurston's L1 Lagrange point", "Everus Harbor")]
    [InlineData("HUR-L1 Green Glade Station", "HUR-L1 Green Glade Station")]
    public void A_pickup_is_cut_to_the_name_of_its_place(string text, string expected) =>
        ContractText.NormalizePickup(text).Should().Be(expected);
}
