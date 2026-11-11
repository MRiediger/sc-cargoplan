using CargoPlan.App.Holo;
using CargoPlan.App.Plans;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public class AppFoldersTests
{
    [Fact]
    public void LocalRoot_is_the_apps_folder_in_local_app_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        AppFolders.LocalRoot.Should().Be(Path.Combine(localAppData, "SC-CargoPlan"));
    }

    [Fact]
    public void The_players_files_lie_in_LocalRoot()
    {
        // AppLog.Path is not here: the test assembly points it at a temp file at load (TestLog), so no test writes the
        // player's log; its default is the same Path.Combine as these.
        Path.GetDirectoryName(PlanStore.DefaultPath).Should().Be(AppFolders.LocalRoot);
        Path.GetDirectoryName(SettingsStore.DefaultPath).Should().Be(AppFolders.LocalRoot);
        Path.GetDirectoryName(HoloRelease.DefaultCacheRoot).Should().Be(AppFolders.LocalRoot);
    }
}
