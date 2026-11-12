using CargoPlan.App.Scanning;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Tests;

/// <summary>--keep-scans (T10.5, D108): the last 20 scan pictures, named by time, view and result; never a crash, never the player's profile here.</summary>
public sealed class ScanPicturesTests : IDisposable
{
    private static readonly DateTime At = new(2026, 11, 12, 15, 50, 12);

    private static readonly byte[] Png = [1, 2, 3];

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string Pictures => Path.Combine(_folder, "scans");

    [Theory]
    [InlineData((int)ContractView.Offer, ScanReportKind.Added, 3, "20261112-155012-offer-added-3.png")]
    [InlineData((int)ContractView.Accepted, ScanReportKind.Updated, 1, "20261112-155012-accepted-updated-1.png")]
    [InlineData((int)ContractView.Unknown, ScanReportKind.AlreadyInPlan, 2, "20261112-155012-unknown-alreadyinplan-2.png")]
    [InlineData((int)ContractView.Accepted, ScanReportKind.Incomplete, null, "20261112-155012-accepted-incomplete.png")]
    public void The_name_holds_time_view_result_and_number(int view, ScanReportKind kind, int? scanNo, string expected)
    {
        ScanPictures.Name(At, (ContractView)view, new ScanReport(kind, scanNo)).Should().Be(expected);
    }

    [Fact]
    public void Without_a_read_the_name_has_no_view()
    {
        ScanPictures.Name(At, null, new ScanReport(ScanReportKind.NoContractInFrame)).Should().Be("20261112-155012-nocontractinframe.png");
    }

    [Fact]
    public void A_picture_is_written_under_its_name()
    {
        var name = new ScanPictures(Pictures, () => At).Save(Png, ContractView.Offer, new ScanReport(ScanReportKind.Added, 3));

        name.Should().Be("20261112-155012-offer-added-3.png");
        File.ReadAllBytes(Path.Combine(Pictures, name!)).Should().Equal(Png);
    }

    [Fact]
    public void The_folder_keeps_the_last_20_pictures()
    {
        var time = At;
        var pictures = new ScanPictures(Pictures, () => time);

        for (var i = 1; i <= ScanPictures.MaxPictures + 1; i++)
        {
            time = At.AddMinutes(i);
            pictures.Save(Png, ContractView.Offer, new ScanReport(ScanReportKind.Added, i));
        }

        var names = Directory.GetFiles(Pictures).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        names.Should().HaveCount(ScanPictures.MaxPictures);
        names[0].Should().Be("20261112-155212-offer-added-2.png", "the oldest went");
        names[^1].Should().Be("20261112-161112-offer-added-21.png");
    }

    [Fact]
    public void Files_of_other_names_stay()
    {
        Directory.CreateDirectory(Pictures);
        File.WriteAllText(Path.Combine(Pictures, "notes.txt"), "mine");
        File.WriteAllBytes(Path.Combine(Pictures, "mine.png"), Png);
        var time = At;
        var pictures = new ScanPictures(Pictures, () => time);

        for (var i = 1; i <= ScanPictures.MaxPictures + 1; i++)
        {
            time = At.AddMinutes(i);
            pictures.Save(Png, null, new ScanReport(ScanReportKind.NoContractInFrame));
        }

        File.Exists(Path.Combine(Pictures, "notes.txt")).Should().BeTrue();
        File.Exists(Path.Combine(Pictures, "mine.png")).Should().BeTrue();
        Directory.GetFiles(Pictures, "2026*.png").Should().HaveCount(ScanPictures.MaxPictures);
    }

    [Fact]
    public void No_picture_writes_nothing()
    {
        new ScanPictures(Pictures, () => At).Save(null, ContractView.Offer, new ScanReport(ScanReportKind.Busy)).Should().BeNull();

        Directory.Exists(Pictures).Should().BeFalse();
    }

    [Fact]
    public void A_folder_that_cant_be_written_never_throws()
    {
        // A file where the folder should be: keeping pictures never takes the scan down (D82).
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Pictures, "in the way");

        var name = new ScanPictures(Pictures, () => At).Save(Png, ContractView.Offer, new ScanReport(ScanReportKind.Added, 1));

        name.Should().BeNull();
        TestLog.Text().Should().Contain("Scan picture");
    }

    [Fact]
    public void The_default_folder_lies_next_to_the_plan()
    {
        ScanPictures.DefaultFolder.Should().Be(Path.Combine(AppFolders.LocalRoot, "scans"));
    }
}
