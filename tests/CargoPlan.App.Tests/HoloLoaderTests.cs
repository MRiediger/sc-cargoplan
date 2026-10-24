using System.Numerics;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The hologram's life cycle (D63, T7.4c-2 plan): start, ship change, the switch, the look, a withdrawal. Real
/// <see cref="HoloSource"/> and view model, a fake network, a fresh temp folder per test.
/// </summary>
public sealed class HoloLoaderTests : IDisposable
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");
    private static readonly Ship C2 = ShipLoader.Load("c2_hercules");
    private static readonly byte[] HullAFile = Cube(30);
    private static readonly byte[] C2File = Cube(29);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHttp.FakeHandler _handler = new();
    private readonly OverlayViewModel _viewModel = new(HullA, ShipLoader.LoadAll()) { OutlineMode = ShipOutlineMode.HoloHiddenline };
    private readonly HoloLoader _loader;

    public HoloLoaderTests()
    {
        _loader = new HoloLoader(_viewModel, new HoloSource(_handler, _folder, new FakeHttp.FixedClock(DateTimeOffset.UtcNow), TimeSpan.FromSeconds(5), "1.0"));
        _handler.Serve("manifest.json", FakeHttp.Manifest((HullA.FleetyardsSlug!, HullAFile), (C2.FleetyardsSlug!, C2File)));
        _handler.Serve(HullA.FleetyardsSlug + ".holo", HullAFile);
        _handler.Serve(C2.FleetyardsSlug + ".holo", C2File);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static byte[] Cube(float length)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        Vector3[] hull = [.. vertices.Select(v => new Vector3((v.X - 0.5f) * length, (v.Y - 0.5f) * 12, v.Z * 8))];
        return HoloFixtures.Write(hull, triangles, HoloFixtures.CubeEdges(), 4, 8, length);
    }

    private string CachedFile(Ship ship) => Path.Combine(_folder, ship.FleetyardsSlug + ".holo");

    private void Cache(Ship ship, byte[] data)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(CachedFile(ship), data);
    }

    [Fact]
    public async Task At_the_start_the_hologram_is_fetched_and_shown()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, download: true);

        _handler.Requests.Should().HaveCount(2);
        _viewModel.Holo.Should().NotBeNull();
        _viewModel.Holo!.ShipId.Should().Be(HullA.Id);
        _viewModel.Holo.Look.Should().Be(ShipOutlineMode.HoloHiddenline);
        _viewModel.Holo.Contour.Should().NotBeNull();
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task A_ship_change_cancels_the_running_download_and_shows_the_new_ship_only()
    {
        _handler.Hang(HullA.FleetyardsSlug + ".holo");

        var first = _loader.RequestAsync(HullA, ShipOutlineMode.HoloShaded, true);
        await WaitFor(() => _handler.Requests.Any(r => r.Uri.EndsWith(HullA.FleetyardsSlug + ".holo")));
        _viewModel.ChangeShip(C2);
        var second = _loader.RequestAsync(C2, ShipOutlineMode.HoloShaded, true);
        await Task.WhenAll(first, second);

        _handler.Cancelled.Should().Be(1);
        _viewModel.OutlineMode = ShipOutlineMode.HoloShaded;
        _viewModel.Holo!.ShipId.Should().Be(C2.Id);
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Downloads_off_shows_a_cached_hologram_and_sends_nothing()
    {
        Cache(HullA, HullAFile);

        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, download: false);

        _handler.Requests.Should().BeEmpty();
        _viewModel.Holo!.ShipId.Should().Be(HullA.Id);
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Downloads_off_without_a_cached_file_shows_the_outline_silently()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, download: false);

        _handler.Requests.Should().BeEmpty();
        _viewModel.Holo.Should().BeNull();
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task Turning_downloads_on_again_fetches_what_the_cache_lacked()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, download: false);

        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, download: true);

        _handler.Requests.Should().HaveCount(2);
        _viewModel.Holo.Should().NotBeNull();
    }

    [Fact]
    public async Task Another_look_of_the_same_ship_needs_neither_network_nor_disk()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        var hiddenLine = _viewModel.Holo;
        Directory.Delete(_folder, recursive: true);

        _viewModel.OutlineMode = ShipOutlineMode.HoloFlat;
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloFlat, true);

        _handler.Requests.Should().HaveCount(2);
        _viewModel.Holo!.Look.Should().Be(ShipOutlineMode.HoloFlat);
        _viewModel.Holo.Mesh.Should().BeSameAs(hiddenLine!.Mesh);
        _viewModel.Holo.Contour.Should().BeNull();
    }

    [Fact]
    public async Task The_outline_look_clears_the_hologram_without_a_request()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        _viewModel.OutlineMode = ShipOutlineMode.Outline;
        await _loader.RequestAsync(HullA, ShipOutlineMode.Outline, true);
        _viewModel.OutlineMode = ShipOutlineMode.HoloHiddenline;

        _handler.Requests.Should().HaveCount(2);
        _viewModel.Holo.Should().BeNull();
    }

    [Fact]
    public async Task A_withdrawal_deletes_the_cached_file_and_shows_the_outline_without_a_notice()
    {
        _handler.Serve("manifest.json", FakeHttp.Manifest((C2.FleetyardsSlug!, C2File)));
        Cache(HullA, HullAFile);

        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        File.Exists(CachedFile(HullA)).Should().BeFalse();
        _viewModel.Holo.Should().BeNull();
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task The_first_failure_of_a_run_gives_one_notice_a_hologram_clears_it_and_it_never_comes_back()
    {
        _handler.Answer(HullA.FleetyardsSlug + ".holo", () => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));

        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Notices.Should().Equal(OverlayViewModel.HoloNoticeText("github.com answered 404 for misc-hull-a.holo"));

        _viewModel.ChangeShip(C2);
        await _loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Notices.Should().BeEmpty();

        _viewModel.ChangeShip(HullA);
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Holo.Should().BeNull();
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task A_ship_without_a_fleetyards_slug_shows_the_outline_without_a_request()
    {
        await _loader.RequestAsync(HullA with { FleetyardsSlug = null }, ShipOutlineMode.HoloHiddenline, true);

        _handler.Requests.Should().BeEmpty();
        _viewModel.Holo.Should().BeNull();
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }
}
