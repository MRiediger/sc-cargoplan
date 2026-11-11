using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Holo;

/// <summary>
/// Brings the hologram of the current ship and look to the overlay (D63), like the scan runner brings a contract:
/// called on the UI thread, fetch, reading and preparing run off it, the result comes back here (await resumes
/// in WPF's synchronization context) and goes to <see cref="OverlayViewModel.ShowHolo"/>. A new request cancels the one
/// running, so a quick ship change never shows the ship before. The mesh read last is kept: another look of the same
/// ship needs neither network nor disk. Every failure ends in the outline; the first of a run gives one notice. After a
/// tag change (D95) one more notice stands while the first hologram loads, or for the run if the old cache stays.
/// </summary>
public sealed class HoloLoader
{
    private readonly OverlayViewModel _viewModel;
    private readonly HoloSource _source;
    private CancellationTokenSource? _running;
    private (string Slug, HoloMesh Mesh)? _read;
    private bool _noticeShown;

    // The tag-change notice names an old cache that stays: it stays for the run, not only until a hologram shows (K1).
    private bool _updateStays;

    public HoloLoader(OverlayViewModel viewModel, HoloSource source)
    {
        _viewModel = viewModel;
        _source = source;

        // Raised inside GetAsync on this (the UI) thread, after the manifest and before the first file downloads.
        source.TagChanged += change => ShowUpdate(change.Problem);
    }

    /// <summary>
    /// At the start: another tag's cache goes at once when holograms aren't wanted now, the switch off or no hologram
    /// look (D95); no access. Otherwise the current tag's manifest decides, at the first request.
    /// </summary>
    public void RetireUnneeded(ShipOutlineMode look, bool download)
    {
        if ((!download || !look.IsHologram()) && _source.RetireOtherTags() is { } problem)
        {
            ShowUpdate(problem);
        }
    }

    /// <summary>At the start, on a ship change and when look or switch change. Not a hologram look: the outline at once, no access.</summary>
    /// <param name="download">The switch "Download ship holograms"; off reads only the cache (Martin, T7.4c-2).</param>
    public async Task RequestAsync(Ship ship, ShipOutlineMode look, bool download)
    {
        // Not disposed: the request it cancels may still ask its token, and without a timer it holds nothing to free.
        _running?.Cancel();
        var running = _running = new CancellationTokenSource();
        var cancel = running.Token;
        if (!look.IsHologram() || ship.ModelKey is not { } slug)
        {
            _viewModel.ShowHolo(null);
            _viewModel.SetNotice(NoticeSource.Holo, null);
            ClearUpdate();
            return;
        }

        try
        {
            var mesh = _read is { } read && read.Slug == slug ? read.Mesh : await ReadAsync(slug, download, cancel);
            cancel.ThrowIfCancellationRequested();
            if (mesh is null)
            {
                _viewModel.ShowHolo(null);
                return;
            }

            _read = (slug, mesh);
            var prepared = await Task.Run(() => PreparedHolo.Prepare(ship, mesh, look), cancel);
            cancel.ThrowIfCancellationRequested();
            _viewModel.ShowHolo(prepared);
            _viewModel.SetNotice(NoticeSource.Holo, null);
            ClearUpdate();
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Another request took over; it shows its own ship.
        }
    }

    /// <summary>The ship's mesh from <see cref="HoloSource"/>, or null for the outline (with the notice where something went wrong).</summary>
    private async Task<HoloMesh?> ReadAsync(string slug, bool download, CancellationToken cancel)
    {
        switch (await _source.GetAsync(slug, download, cancel))
        {
            case HoloFetch.Found found:
                try
                {
                    var mesh = await Task.Run(() => HoloReader.Read(found.Data), cancel);
                    if (found.Problem is { } problem)
                    {
                        Notice(problem);
                    }

                    return mesh;
                }
                catch (HoloFormatException ex)
                {
                    AppLog.Write("Holo", $"{ex.Message} [{ex.GetType().Name}]");
                    Notice(ex.Message);
                    return null;
                }

            case HoloFetch.Failed failed:
                Notice(failed.Reason);
                return null;

            default:
                return null;
        }
    }

    // One per run: a player offline sees why once, not on every ship change.
    private void Notice(string reason)
    {
        if (!_noticeShown)
        {
            _noticeShown = true;
            _viewModel.SetNotice(NoticeSource.Holo, OverlayViewModel.HoloNoticeText(reason));
        }
    }

    private void ShowUpdate(string? problem)
    {
        _updateStays = problem is not null;
        _viewModel.SetNotice(NoticeSource.HoloUpdate, OverlayViewModel.HoloUpdateNoticeText(problem));
    }

    // Like the hologram notice: gone when a hologram shows or the look is no hologram, unless it names a problem.
    private void ClearUpdate()
    {
        if (!_updateStays)
        {
            _viewModel.SetNotice(NoticeSource.HoloUpdate, null);
        }
    }
}
