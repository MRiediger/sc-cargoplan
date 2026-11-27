using CargoPlan.App.Vr;
using Valve.VR;

namespace CargoPlan.App.Tests;

// T14.2, D131: the double buffer. The upload goes into the hidden overlay; it shows, and the other hides, only after its
// VREvent_ImageLoaded (SetOverlayRaw is asynchronous, and one overlay alone flickers on every texture change, S14.0).
public class VrOverlayHostTests
{
    private const ulong A = 1;
    private const ulong B = 2;

    private readonly FakeOpenVr _vr = new();
    private readonly VrOverlayHost _host;

    public VrOverlayHostTests()
    {
        _host = new VrOverlayHost(_vr);
        _host.Create().Should().Be(EVROverlayError.None);
    }

    [Fact]
    public void The_first_frame_goes_to_A_and_shows_only_after_A_has_loaded()
    {
        _host.Ready.Should().BeTrue();

        _host.Upload(Frame());

        _host.Ready.Should().BeFalse("the next frame waits for this one");
        _host.Poll().Should().Be(0);
        Shown().Should().BeEmpty();

        _vr.Overlay(A, EVREventType.VREvent_ImageLoaded);
        _host.Poll().Should().Be(1);

        Shown().Should().Equal("Show 1");
        _host.Ready.Should().BeTrue();
    }

    [Fact]
    public void The_next_frame_goes_to_the_hidden_overlay_and_flips_after_it_has_loaded()
    {
        Flip(A);

        _host.Upload(Frame());
        _vr.Calls.Last().Should().Be("Raw 2 4x2");
        _host.Poll();
        Shown().Should().Equal("Show 1");

        _vr.Overlay(B, EVREventType.VREvent_ImageLoaded);
        _host.Poll();

        // The new one first, so the headset is never without a picture.
        Shown().Should().Equal("Show 1", "Show 2", "Hide 1");
    }

    [Fact]
    public void Frames_alternate_between_the_two_overlays()
    {
        Flip(A);
        Flip(B);

        _host.Upload(Frame());

        _vr.Calls.Last().Should().Be("Raw 1 4x2");
        _vr.Overlay(A, EVREventType.VREvent_ImageLoaded);
        _host.Poll();
        Shown().Should().Equal("Show 1", "Show 2", "Hide 1", "Show 1", "Hide 2");
    }

    [Fact]
    public void A_late_event_of_the_shown_overlay_flips_nothing()
    {
        Flip(A);
        _host.Upload(Frame());

        _vr.Overlay(A, EVREventType.VREvent_ImageLoaded);
        _host.Poll().Should().Be(0);

        Shown().Should().Equal("Show 1");
        _host.Ready.Should().BeFalse();
    }

    [Fact]
    public void A_failed_load_shows_nothing_and_takes_the_next_frame()
    {
        _host.Upload(Frame());

        _vr.Overlay(A, EVREventType.VREvent_ImageFailed);
        _host.Poll().Should().Be(0);

        Shown().Should().BeEmpty();
        _host.Ready.Should().BeTrue();
        _host.Upload(Frame());
        _vr.Calls.Last().Should().Be("Raw 1 4x2", "A is still the hidden one");
    }

    [Fact]
    public void A_refused_upload_waits_for_nothing()
    {
        _vr.RawResult = EVROverlayError.InvalidParameter;

        _host.Upload(Frame());

        _host.Ready.Should().BeTrue();
    }

    [Fact]
    public void Destroy_forgets_a_load_in_flight()
    {
        _host.Upload(Frame());

        _host.Destroy();
        _host.Create();

        _host.Ready.Should().BeTrue();
        _vr.Overlay(3, EVREventType.VREvent_ImageLoaded);
        _host.Poll().Should().Be(0);
    }

    private static VrFrame Frame() => new(4, 2);

    private void Flip(ulong into)
    {
        _host.Upload(Frame());
        _vr.Overlay(into, EVREventType.VREvent_ImageLoaded);
        _host.Poll().Should().Be(1);
    }

    private IReadOnlyList<string> Shown() => [.. _vr.Calls.Where(c => c.StartsWith("Show", StringComparison.Ordinal) || c.StartsWith("Hide", StringComparison.Ordinal))];
}
