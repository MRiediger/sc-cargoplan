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
    public void A_build_without_a_tag_is_a_dev_build()
    {
        // Directory.Build.props: 0.0.0-dev unless publish.ps1 or release.yml pass -p:Version.
        AppVersion.Current.Should().Be(AppVersion.DevVersion);
        AppVersion.IsDevBuild.Should().BeTrue();
    }
}
