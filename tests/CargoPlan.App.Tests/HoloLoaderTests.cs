using System.Numerics;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The hologram's life cycle (D63, T7.4c-2 plan, D114): start, ship change, the switch, the look, what the ship data
/// offer. Real <see cref="HoloSource"/> and view model, a fake network, a fresh temp folder per test.
/// </summary>
public sealed class HoloLoaderTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");
    private static readonly Ship C2 = FrozenShips.Load("c2_hercules");
    private static readonly byte[] HullAFile = Cube(30);
    private static readonly byte[] C2File = Cube(29);

    // The cache root of this test alone (D95); _folder is the current data version's.
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _folder;
    private readonly FakeHttp.FakeHandler _handler = new();
    private readonly OverlayViewModel _viewModel = new(HullA, FrozenShips.LoadAll()) { OutlineMode = ShipOutlineMode.HoloHiddenline };
    private readonly HoloLoader _loader;

    // Every notice line the view model showed, in order: a notice that came and went shows here, not in Notices.
    private readonly List<string> _shown = [];

    public HoloLoaderTests()
    {
        _folder = HoloSource.CacheFolder(_root, FakeHttp.DataVersion);
        _loader = NewLoader();
        _handler.Serve(HullA.ModelKey + ".holo", HullAFile);
        _handler.Serve(C2.ModelKey + ".holo", C2File);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.Notices))
            {
                _shown.AddRange(_viewModel.Notices.Except(_shown));
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Updated => OverlayViewModel.HoloUpdateNoticeText(null);

    private string OldTag => Path.Combine(_root, "holo-v3");

    private static HoloOffer BothShips => FakeHttp.Holo((HullA.ModelKey!, HullAFile), (C2.ModelKey!, C2File));

    /// <summary>A loader as at a start: its source looks at the cache root when it is made; the data offer both ships.</summary>
    private HoloLoader NewLoader(HoloOffer? holo = null) => new(
        _viewModel,
        new HoloSource(_handler, _root, new Uri("https://github.com/MRiediger/sc-cargoplan-shipdata/"), FakeHttp.Data(holo ?? BothShips), TimeSpan.FromSeconds(5), "1.0"));

    /// <summary>The cache of v0.5.1: holo\holo-v3 with a file in it.</summary>
    private void OldCache()
    {
        Directory.CreateDirectory(OldTag);
        File.WriteAllBytes(Path.Combine(OldTag, HullA.ModelKey + ".holo"), HullAFile);
    }

    private static byte[] Cube(float length)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        Vector3[] hull = [.. vertices.Select(v => new Vector3((v.X - 0.5f) * length, (v.Y - 0.5f) * 12, v.Z * 8))];
        return HoloFixtures.Write(hull, triangles, HoloFixtures.CubeEdges(), 4, 8, length);
    }

    private string CachedFile(Ship ship) => Path.Combine(_folder, ship.ModelKey + ".holo");

    private void Cache(Ship ship, byte[] data)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(CachedFile(ship), data);
    }

    [Fact]
    public async Task At_the_start_the_hologram_is_fetched_and_shown()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, download: true);

        _handler.Requests.Should().ContainSingle().Which.Uri.Should().EndWith("/ships-2026.11.1/misc-hull-a.holo");
        _viewModel.Holo.Should().NotBeNull();
        _viewModel.Holo!.ShipId.Should().Be(HullA.Id);
        _viewModel.Holo.Look.Should().Be(ShipOutlineMode.HoloHiddenline);
        _viewModel.Holo.Contour.Should().NotBeNull();
        _viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public async Task A_ship_change_cancels_the_running_download_and_shows_the_new_ship_only()
    {
        _handler.Hang(HullA.ModelKey + ".holo");

        var first = _loader.RequestAsync(HullA, ShipOutlineMode.HoloShaded, true);
        await WaitFor(() => _handler.Requests.Any(r => r.Uri.EndsWith(HullA.ModelKey + ".holo")));
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

        _handler.Requests.Should().ContainSingle();
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

        _handler.Requests.Should().ContainSingle();
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

        _handler.Requests.Should().ContainSingle();
        _viewModel.Holo.Should().BeNull();
    }

    // Was A_withdrawal_deletes_the_cached_file_…: the manifest stays for the run, a ship it lacks has no hologram.
    [Fact]
    public async Task A_ship_the_manifest_does_not_list_shows_the_outline_without_a_request_or_notice()
    {
        await NewLoader(FakeHttp.Holo((C2.ModelKey!, C2File))).RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        _handler.Requests.Should().BeEmpty();
        _viewModel.Holo.Should().BeNull();
        _shown.Should().BeEmpty();
    }

    // The failure was a 404 until T12.4; a 404 is now a release that is gone (below).
    [Fact]
    public async Task The_first_failure_of_a_run_gives_one_notice_a_hologram_clears_it_and_it_never_comes_back()
    {
        _handler.Answer(HullA.ModelKey + ".holo", () => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Notices.Should().Equal(OverlayViewModel.HoloNoticeText("github.com answered 503 for misc-hull-a.holo"));

        _viewModel.ChangeShip(C2);
        await _loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Notices.Should().BeEmpty();

        _viewModel.ChangeShip(HullA);
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Holo.Should().BeNull();
        _viewModel.Notices.Should().BeEmpty();
    }

    // Festlegung 5 of T12.4: a deleted release shows the outline for that ship, no notice; the update line is the way back.
    [Fact]
    public async Task A_404_shows_the_outline_without_a_notice_and_the_next_ship_its_hologram()
    {
        _handler.Answer(HullA.ModelKey + ".holo", () => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));

        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Holo.Should().BeNull();
        _viewModel.ChangeShip(C2);
        await _loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Holo!.ShipId.Should().Be(C2.Id);
        _viewModel.ChangeShip(HullA);
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        _shown.Should().BeEmpty();
        _handler.Requests.Count(r => r.Uri.EndsWith(HullA.ModelKey + ".holo")).Should().Be(1);
    }

    // D63 kill switch and R1 (a format this app doesn't read): no request, no notice, however often the ship changes;
    // the old cache goes at the start, silently.
    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    public async Task Without_holograms_on_offer_ship_changes_show_the_outline_without_a_request_or_notice(int? format)
    {
        OldCache();
        var loader = NewLoader(format is { } n ? new HoloOffer.UnknownFormat(n) : new HoloOffer.NotOffered());

        loader.RetireUnneeded(ShipOutlineMode.HoloHiddenline, download: true);
        foreach (var ship in (Ship[])[HullA, C2, HullA])
        {
            _viewModel.ChangeShip(ship);
            await loader.RequestAsync(ship, ShipOutlineMode.HoloHiddenline, true);
        }

        Directory.Exists(OldTag).Should().BeFalse();
        _handler.Requests.Should().BeEmpty();
        _viewModel.Holo.Should().BeNull();
        _shown.Should().BeEmpty();
    }

    [Fact]
    public async Task A_ship_without_a_model_key_shows_the_outline_without_a_request()
    {
        await _loader.RequestAsync(HullA with { ModelKey = null }, ShipOutlineMode.HoloHiddenline, true);

        _handler.Requests.Should().BeEmpty();
        _viewModel.Holo.Should().BeNull();
    }

    // --- Cache change (D95): one notice after an update from v0.5.x or another data version, gone with the first hologram (K1) ---

    [Fact]
    public async Task After_a_tag_change_the_notice_stands_while_the_first_hologram_loads()
    {
        OldCache();
        var loader = NewLoader();
        _handler.Hang(HullA.ModelKey + ".holo");

        var running = loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        await WaitFor(() => _handler.Requests.Any(r => r.Uri.EndsWith(HullA.ModelKey + ".holo")));

        _viewModel.Notices.Should().Equal(Updated);
        Directory.Exists(OldTag).Should().BeFalse();
        _viewModel.ChangeShip(C2);
        await Task.WhenAll(running, loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true));
    }

    [Fact]
    public async Task The_tag_change_notice_goes_with_the_first_hologram_and_never_comes_back()
    {
        OldCache();
        var loader = NewLoader();

        await loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.ChangeShip(C2);
        await loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true);

        _shown.Should().Equal(Updated);
        _viewModel.Notices.Should().BeEmpty();
        _viewModel.Holo!.ShipId.Should().Be(C2.Id);
    }

    [Fact]
    public async Task A_failed_delete_keeps_the_notice_with_its_reason_for_the_run()
    {
        OldCache();
        using var locked = File.Open(Path.Combine(OldTag, HullA.ModelKey + ".holo"), FileMode.Open, FileAccess.Read, FileShare.None);
        var loader = NewLoader();

        await loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.ChangeShip(C2);
        await loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true);

        _viewModel.Holo.Should().NotBeNull();
        _viewModel.Notices.Should().ContainSingle().Which.Should().StartWith(OverlayViewModel.HoloUpdateNoticeText("the old cache can't be removed"));
    }

    [Fact]
    public async Task A_first_install_gives_no_tag_change_notice()
    {
        await _loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        _shown.Should().BeEmpty();
    }

    // Was Offline_the_old_tag_stays_and_only_the_hologram_notice_shows: the manifest is local now, so the old cache goes
    // offline too, and both lines say what happened.
    [Fact]
    public async Task Offline_after_a_cache_change_the_old_cache_goes_and_both_notices_show()
    {
        OldCache();
        _handler.Fail(HullA.ModelKey + ".holo");
        var loader = NewLoader();

        await loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        Directory.Exists(OldTag).Should().BeFalse();
        _viewModel.Notices.Should().HaveCount(2).And.Contain(Updated)
            .And.Contain(n => n.StartsWith("Ship hologram not available: no connection", StringComparison.Ordinal));
    }

    // A13: a 404 writes no notice and must not take the cache change's away; the first hologram does.
    [Fact]
    public async Task After_a_cache_change_a_404_leaves_the_notice_until_a_hologram_shows()
    {
        OldCache();
        _handler.Answer(HullA.ModelKey + ".holo", () => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        var loader = NewLoader();

        await loader.RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Notices.Should().Equal(Updated);

        _viewModel.ChangeShip(C2);
        await loader.RequestAsync(C2, ShipOutlineMode.HoloHiddenline, true);
        _viewModel.Notices.Should().BeEmpty();
        _shown.Should().Equal(Updated);
    }

    [Fact]
    public async Task The_next_start_after_the_tag_change_gives_no_notice()
    {
        OldCache();
        await NewLoader().RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);
        _shown.Clear();

        await NewLoader().RequestAsync(HullA, ShipOutlineMode.HoloHiddenline, true);

        _shown.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ShipOutlineMode.HoloHiddenline, false)]
    [InlineData(ShipOutlineMode.Outline, true)]
    [InlineData(ShipOutlineMode.None, true)]
    public void Without_holograms_wanted_the_start_retires_the_other_tag_at_once(ShipOutlineMode look, bool download)
    {
        OldCache();

        NewLoader().RetireUnneeded(look, download);

        Directory.Exists(OldTag).Should().BeFalse();
        _handler.Requests.Should().BeEmpty();
        _shown.Should().BeEmpty();
    }

    [Fact]
    public void With_holograms_wanted_the_start_leaves_the_other_tag_to_the_first_request()
    {
        OldCache();

        NewLoader().RetireUnneeded(ShipOutlineMode.HoloShaded, download: true);

        Directory.Exists(OldTag).Should().BeTrue();
        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public void A_failed_delete_at_the_start_gives_the_notice_with_its_reason()
    {
        OldCache();
        using var locked = File.Open(Path.Combine(OldTag, HullA.ModelKey + ".holo"), FileMode.Open, FileAccess.Read, FileShare.None);

        NewLoader().RetireUnneeded(ShipOutlineMode.Outline, download: true);

        _viewModel.Notices.Should().ContainSingle().Which.Should().StartWith(OverlayViewModel.HoloUpdateNoticeText("the old cache can't be removed"));
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
