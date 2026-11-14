using System.Reflection;

namespace CargoPlan.App.Tests;

/// <summary>The app's own version, as the update check compares it (T11.1, D109).</summary>
public class AppVersionTests
{
    [Theory]
    [InlineData("0.5.0+140be1f", "0.5.0")]
    [InlineData("0.5.0", "0.5.0")]
    [InlineData("0.0.0-dev+abc", "0.0.0-dev")]
    [InlineData(null, "0.0.0")]
    public void The_commit_hash_the_sdk_appends_is_dropped(string? informational, string version)
    {
        AppVersion.WithoutMetadata(informational).Should().Be(version);
    }

    [Fact]
    public void The_app_reports_the_version_the_build_was_given()
    {
        // Directory.Build.props: 0.0.0-dev unless publish.ps1 or release.yml pass -p:Version, which reaches every project
        // of the build, this one too. So the expectation comes from the test's own build: in ci.yml and locally the dev
        // build, under release.yml the tag's version (T11.3: a fixed 0.0.0-dev failed the first tag after T11.1).
        var built = AppVersion.WithoutMetadata(
            typeof(AppVersionTests).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        AppVersion.Current.Should().Be(built);
        AppVersion.IsDevBuild.Should().Be(built == AppVersion.DevVersion);
    }
}
