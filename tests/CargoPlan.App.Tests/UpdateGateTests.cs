using CargoPlan.App.Updates;

namespace CargoPlan.App.Tests;

/// <summary>The decision whether to check and what to offer (T11.1, D109), without Velopack.</summary>
public class UpdateGateTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void Only_a_release_build_with_the_check_on_asks_at_all(bool checkOnStartup, bool isDevBuild, bool checks)
    {
        // A dev build must never see an "update" to 0.x.y (E6); off means no network at all.
        UpdateGate.ShouldCheck(checkOnStartup, isDevBuild).Should().Be(checks);
    }

    [Fact]
    public void A_newer_version_is_offered_with_its_notes()
    {
        UpdateGate.Offer("0.9.0", "0.9.1", "Fixes", skipped: null).Should().Be(new UpdateAvailable("0.9.1", "Fixes"));
    }

    [Theory]
    [InlineData("0.9.0", "0.9.0")]
    [InlineData("0.9.1", "0.9.0")]
    [InlineData("1.0.0", "0.10.0")]
    public void The_same_or_an_older_version_is_not_offered(string current, string found)
    {
        UpdateGate.Offer(current, found, null, null).Should().BeOfType<NoUpdate>();
    }

    [Fact]
    public void Nothing_found_is_no_update()
    {
        UpdateGate.Offer("0.9.0", found: null, null, null).Should().Be(new NoUpdate("nothing newer than 0.9.0"));
    }

    [Fact]
    public void A_skipped_version_is_not_offered()
    {
        UpdateGate.Offer("0.9.0", "0.9.1", null, skipped: "0.9.1").Should().Be(new NoUpdate("0.9.1 skipped (updates.skippedVersion)"));
    }

    [Fact]
    public void Skip_means_this_version_a_newer_one_is_offered_again()
    {
        // E7: not "never again"; the global off is checkOnStartup.
        UpdateGate.Offer("0.9.0", "0.9.2", null, skipped: "0.9.1").Should().Be(new UpdateAvailable("0.9.2", null));
    }

    [Fact]
    public void A_pre_release_is_never_offered()
    {
        // GithubSource filters by GitHub's flag, not the version (S11.0 G7); this catches an rc published without it.
        UpdateGate.Offer("0.9.0", "0.9.1-rc.1", null, null).Should().Be(new NoUpdate("0.9.1-rc.1 is a pre-release"));
    }

    [Fact]
    public void A_release_candidate_is_offered_its_stable_release()
    {
        // At the same number the stable release is the newer (SemVer 2.0.0, rule 11).
        UpdateGate.Offer("0.5.0-rc.1", "0.5.0", null, null).Should().Be(new UpdateAvailable("0.5.0", null));
    }

    [Theory]
    [InlineData("0.9.0+abc1234", "0.9.1", true)]
    [InlineData("0.9.0", "0.9.0+def5678", false)]
    public void Build_metadata_does_not_count(string current, string found, bool offered)
    {
        // The assembly carries "+sha" (Directory.Build.props), Velopack's sq.version doesn't.
        (UpdateGate.Offer(current, found, null, null) is UpdateAvailable).Should().Be(offered);
    }

    [Theory]
    [InlineData("0.9.0", "banana")]
    [InlineData("0.9.0", "0.9")]
    [InlineData("0.9.0", "0.9.1.2")]
    [InlineData("0.9.0", "v0.9.1")]
    [InlineData("unknown", "0.9.1")]
    public void An_unreadable_version_is_no_update(string current, string found)
    {
        UpdateGate.Offer(current, found, null, null).Should().BeOfType<NoUpdate>();
    }

    [Fact]
    public void An_unreadable_skipped_version_skips_nothing()
    {
        UpdateGate.Offer("0.9.0", "0.9.1", null, skipped: "later").Should().Be(new UpdateAvailable("0.9.1", null));
    }

    [Theory]
    [InlineData("0.9.1", "0.9.1")]
    [InlineData("0.9.1+abc", "0.9.1")]
    [InlineData("0.5.0-rc.1", "0.5.0-rc.1")]
    [InlineData("10.20.30", "10.20.30")]
    public void A_version_reads_as_major_minor_patch_and_an_optional_pre_release(string text, string normalized)
    {
        ReleaseVersion.TryParse(text, out var version).Should().BeTrue();
        version.ToString().Should().Be(normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0.9.1-")]
    [InlineData("-0.9.1")]
    [InlineData("0.9.1 ")]
    public void Not_a_version(string? text)
    {
        ReleaseVersion.TryParse(text, out _).Should().BeFalse();
    }
}
