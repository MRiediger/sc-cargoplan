using System.Collections.Concurrent;
using System.Diagnostics;
using CargoPlan.App.Vr;
using Valve.VR;

namespace CargoPlan.App.Tests;

// T14.1, D131: the session's state machine and its VR thread, without SteamVR.
public sealed class OpenVrSessionTests : IDisposable
{
    private const string A = "cargoplan.vr.a";
    private const string B = "cargoplan.vr.b";

    private static readonly string[] Connected =
    [
        "TryLoad", "Init VRApplication_Background", $"Create {A}", $"Create {B}", "HeadPose",
        "Width 1 0.5", "Alpha 1 1", "Transform 1", "Width 2 0.5", "Alpha 2 1", "Transform 2", "Raw 1 512x256",
    ];

    private static readonly string[] Down = ["Destroy 1", "Destroy 2", "Shutdown"];

    private readonly FakeOpenVr _vr = new();
    private readonly ConcurrentQueue<VrReport> _reports = new();
    private readonly OpenVrSession _session;

    public OpenVrSessionTests() => _session = new OpenVrSession(_vr, _reports.Enqueue, VrOffset.Default);

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Turning_on_connects_as_a_background_app_with_two_overlays_and_the_test_image_in_A()
    {
        _session.Start();

        WaitFor(() => _reports.Contains(new VrReport(true, null)));
        _vr.Calls.Should().Equal(Connected);

        // A shows only after its image has loaded (SetOverlayRaw is asynchronous, S14.0); B stays hidden until T14.2.
        _vr.Overlay(1, EVREventType.VREvent_ImageLoaded);
        WaitFor(() => _vr.Calls.Contains("Show 1"));
        _vr.Calls.Should().Equal([.. Connected, "Show 1"]);
    }

    [Fact]
    public void Every_call_runs_on_one_thread_of_its_own()
    {
        _session.Start();
        WaitFor(() => _reports.Contains(new VrReport(true, null)));
        _session.Stop();
        WaitFor(() => !_session.IsRunning);

        _vr.Threads.Should().ContainSingle().Which.Should().NotBe(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void Quit_acknowledges_destroys_both_overlays_and_shuts_down()
    {
        Connect();

        _vr.SystemEvents.Enqueue(EVREventType.VREvent_Quit);

        WaitFor(() => !_session.IsRunning);
        _vr.Calls.Should().Equal([.. Connected, "AcknowledgeQuit", .. Down]);
        _reports.Last().Should().Be(new VrReport(false, OpenVrSession.ClosedText));
    }

    [Fact]
    public void Turning_on_again_after_a_quit_connects_in_the_same_session()
    {
        Connect();
        _vr.SystemEvents.Enqueue(EVREventType.VREvent_Quit);
        WaitFor(() => !_session.IsRunning);

        _session.Start();

        WaitFor(() => _reports.Last() == new VrReport(true, null));
        _vr.Calls.Count(c => c == "Init VRApplication_Background").Should().Be(2);
    }

    [Fact]
    public void A_quit_while_A_is_still_loading_shuts_down_and_shows_nothing()
    {
        // E2: SteamVR closing right after the connect; two expectations are open (ImageLoaded, Show).
        Connect();

        _vr.SystemEvents.Enqueue(EVREventType.VREvent_Quit);
        WaitFor(() => !_session.IsRunning);

        // A late ImageLoaded finds nobody listening.
        _vr.Overlay(1, EVREventType.VREvent_ImageLoaded);
        Thread.Sleep(10 * (int)OpenVrSession.PollInterval.TotalMilliseconds);
        _vr.Calls.Should().Equal([.. Connected, "AcknowledgeQuit", .. Down]);
        _reports.Last().Should().Be(new VrReport(false, OpenVrSession.ClosedText));
    }

    [Fact]
    public void A_quit_is_acknowledged_within_a_second_while_the_ui_thread_is_blocked()
    {
        // S14.0 G6: SteamVR ends a process that doesn't acknowledge within ≈ 5 s. The report reaches a blocked UI thread
        // like Dispatcher.InvokeAsync: queued, never waited for.
        using var ui = new ManualResetEventSlim();
        using var session = new OpenVrSession(_vr, report => Task.Run(() => { ui.Wait(); _reports.Enqueue(report); }), VrOffset.Default);
        session.Start();
        WaitFor(() => _vr.Calls.Contains("Raw 1 512x256"));

        var watch = Stopwatch.StartNew();
        _vr.SystemEvents.Enqueue(EVREventType.VREvent_Quit);
        WaitFor(() => _vr.Calls.Contains("Shutdown"));

        watch.ElapsedMilliseconds.Should().BeLessThan(1000);
        _reports.Should().BeEmpty();
        ui.Set();
        WaitFor(() => _reports.Count == 2);
    }

    [Fact]
    public void Without_SteamVR_it_stays_off_with_a_notice_and_creates_nothing()
    {
        _vr.InitResult = EVRInitError.Init_NoServerForBackgroundApp;

        _session.Start();

        WaitFor(() => !_reports.IsEmpty && !_session.IsRunning);
        _reports.Should().Equal(new VrReport(false, OpenVrSession.NotRunningText));
        _vr.Calls.Should().Equal("TryLoad", "Init VRApplication_Background");
    }

    [Fact]
    public void A_missing_dll_stays_off_with_its_own_notice()
    {
        _vr.Loadable = false;

        _session.Start();

        WaitFor(() => !_reports.IsEmpty && !_session.IsRunning);
        _reports.Should().Equal(new VrReport(false, OpenVrSession.DllMissingText));
        _vr.Calls.Should().Equal("TryLoad");
    }

    [Theory]
    [InlineData(typeof(DllNotFoundException))]
    [InlineData(typeof(EntryPointNotFoundException))]
    [InlineData(typeof(BadImageFormatException))]
    public void A_dll_that_fails_on_the_first_call_gives_the_same_notice(Type error)
    {
        // The exception text is localised by Windows (S14.0 G6): our own English line instead.
        _vr.InitThrows = (Exception)Activator.CreateInstance(error)!;

        _session.Start();

        WaitFor(() => !_reports.IsEmpty && !_session.IsRunning);
        _reports.Should().Equal(new VrReport(false, OpenVrSession.DllMissingText));
    }

    [Fact]
    public void A_second_instance_gets_key_in_use_and_shuts_down()
    {
        _vr.CreateResults[A] = EVROverlayError.KeyInUse;

        _session.Start();

        WaitFor(() => !_reports.IsEmpty && !_session.IsRunning);
        _reports.Should().Equal(new VrReport(false, OpenVrSession.SecondInstanceText));
        _vr.Calls.Should().Equal("TryLoad", "Init VRApplication_Background", $"Create {A}", "Shutdown");
    }

    [Fact]
    public void Key_in_use_on_the_second_overlay_destroys_the_first()
    {
        _vr.CreateResults[B] = EVROverlayError.KeyInUse;

        _session.Start();

        WaitFor(() => !_reports.IsEmpty && !_session.IsRunning);
        _reports.Should().Equal(new VrReport(false, OpenVrSession.SecondInstanceText));
        _vr.Calls.Should().Equal("TryLoad", "Init VRApplication_Background", $"Create {A}", $"Create {B}", "Destroy 1", "Shutdown");
    }

    [Fact]
    public void Any_other_refusal_names_the_error()
    {
        _vr.InitResult = EVRInitError.Init_HmdNotFound;

        _session.Start();

        WaitFor(() => !_reports.IsEmpty && !_session.IsRunning);
        _reports.Should().Equal(new VrReport(false, OpenVrSession.RefusedText("Init_HmdNotFound")));
    }

    [Fact]
    public void Turning_off_shuts_down_and_clears_the_notice()
    {
        Connect();

        _session.Stop();

        WaitFor(() => !_session.IsRunning);
        _vr.Calls.Should().Equal([.. Connected, .. Down]);
        _reports.Last().Should().Be(new VrReport(false, null));
    }

    [Fact]
    public void Never_turned_on_it_calls_nothing_and_has_no_thread()
    {
        // Invariant 3: VR off = no OpenVR call, no thread.
        _session.Stop();
        _session.Dispose();

        _vr.Calls.Should().BeEmpty();
        _reports.Should().BeEmpty();
        _session.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Dispose_while_connected_shuts_down_before_it_returns()
    {
        Connect();

        _session.Dispose();

        _session.IsRunning.Should().BeFalse();
        _vr.Calls.Should().Equal([.. Connected, .. Down]);
    }

    private void Connect()
    {
        _session.Start();
        WaitFor(() => _reports.Contains(new VrReport(true, null)));
    }

    private static void WaitFor(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "the VR thread should have got there");
            Thread.Sleep(5);
        }
    }
}
