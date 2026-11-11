using CargoPlan.App.Win32;
using CargoPlan.Ocr;

namespace CargoPlan.App.Tests;

public class GameWindowTests
{
    // A name no process has, so the test holds whether Star Citizen runs or not.
    [Fact]
    public void Without_the_game_process_there_is_no_window_and_no_exception() =>
        GameWindow.Find("CargoPlanTestNoSuchProcess").Should().BeNull();

    // The S10.0 regions (ScanRegionTests), moved by where the client area lies on the screen.
    [Theory]
    [InlineData(0, 0, 3440, 1440, 1300, 210, 1560, 1000)]
    [InlineData(100, 50, 1920, 1080, 745, 207, 1170, 750)]
    [InlineData(-1920, 0, 1920, 1080, -1275, 157, 1170, 750)] // a monitor left of the primary one
    public void The_scan_region_lies_in_the_client_area_on_the_screen(
        int clientX, int clientY, int width, int height, int x, int y, int regionWidth, int regionHeight) =>
        GameWindow.RegionOnScreen(new PixelRect(clientX, clientY, width, height))
            .Should().Be(new PixelRect(x, y, regionWidth, regionHeight));
}
