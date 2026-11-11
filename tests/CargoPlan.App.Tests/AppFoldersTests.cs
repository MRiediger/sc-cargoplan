using CargoPlan.App.Holo;
using CargoPlan.App.Plans;
using CargoPlan.App.Scanning;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public class AppFoldersTests
{
    [Fact]
    public void Profile_is_the_apps_folder_in_local_app_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        AppFolders.Profile.Should().Be(Path.Combine(localAppData, "SC-CargoPlan"));
    }

    [Fact]
    public void The_players_files_lie_in_LocalRoot()
    {
        // AppLog.Path is not here: the test assembly points it at a temp file at load (TestLog), so no test writes the
        // player's log; its default is the same Path.Combine as these.
        Path.GetDirectoryName(PlanStore.DefaultPath).Should().Be(AppFolders.LocalRoot);
        Path.GetDirectoryName(SettingsStore.DefaultPath).Should().Be(AppFolders.LocalRoot);
        Path.GetDirectoryName(ScanLog.DefaultPath).Should().Be(AppFolders.LocalRoot);
        Path.GetDirectoryName(HoloRelease.DefaultCacheRoot).Should().Be(AppFolders.LocalRoot);
    }
}

/// <summary>
/// <see cref="AppFolders.Use"/> moves every file of the run (T10.4a, B1). It changes static state the other tests read, so
/// it runs alone, after the parallel collections, and puts everything back.
/// Source: https://xunit.net/docs/running-tests-in-parallel ("Opting a test collection out of parallelism")
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AppFoldersCollection
{
    public const string Name = "App folders";
}

[Collection(AppFoldersCollection.Name)]
public sealed class DataDirTests
{
    [Fact]
    public void Use_moves_every_file_of_the_run_into_the_folder_and_creates_it()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"cargoplan-data-dir-{Guid.NewGuid():N}");
        try
        {
            AppFolders.Use(folder);

            Directory.Exists(folder).Should().BeTrue();
            AppFolders.LocalRoot.Should().Be(folder);
            SettingsStore.DefaultPath.Should().Be(Path.Combine(folder, "settings.json"));
            PlanStore.DefaultPath.Should().Be(Path.Combine(folder, "plan.json"));
            ScanLog.DefaultPath.Should().Be(Path.Combine(folder, "scans.log"));
            HoloRelease.DefaultCacheRoot.Should().Be(Path.Combine(folder, "holo"));
            HoloRelease.DefaultCacheFolder.Should().Be(Path.Combine(folder, "holo", HoloRelease.Tag));
            AppLog.Path.Should().Be(Path.Combine(folder, "cargoplan.log"));
        }
        finally
        {
            AppFolders.UseProfile();
            TestLog.Redirect();
            Directory.Delete(folder, recursive: true);
        }

        AppFolders.LocalRoot.Should().Be(AppFolders.Profile);
    }
}
