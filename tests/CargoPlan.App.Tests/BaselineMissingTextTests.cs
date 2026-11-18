using CargoPlan.Core.ShipData;

namespace CargoPlan.App.Tests;

/// <summary>The start without the built-in ship data (T12.5, R5): what went wrong, and what helps.</summary>
public sealed class BaselineMissingTextTests
{
    [Fact]
    public void The_message_names_the_file_the_reason_and_the_remedy()
    {
        var folder = Path.Combine(Path.GetTempPath(), "cargoplan-no-baseline-" + Guid.NewGuid().ToString("N"), ActiveShipData.BaselineFolderName);
        var missing = FluentActions.Invoking(() => ActiveShipData.ReadBaseline(folder)).Should().Throw<ShipDataFormatException>().Which;

        App.BaselineMissingText(missing).Should().Be(
            @"The ship data next to SC-CargoPlan are missing or damaged (ship-data\manifest.json: missing). Download SC-CargoPlan again.");
    }
}
