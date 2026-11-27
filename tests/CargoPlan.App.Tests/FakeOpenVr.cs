using System.Collections.Concurrent;
using System.Globalization;
using CargoPlan.App.Vr;
using Valve.VR;

namespace CargoPlan.App.Tests;

/// <summary>
/// OpenVR without SteamVR (T14.1): records every call with its thread and answers with what the test set. Handles count
/// from 1 in the order of creation. Polls are not recorded, only what they hand out.
/// </summary>
internal sealed class FakeOpenVr : IOpenVr
{
    private readonly ConcurrentQueue<(string Call, int Thread)> _calls = new();
    private ulong _next;

    public bool Loadable { get; set; } = true;

    public EVRInitError InitResult { get; set; } = EVRInitError.None;

    public Exception? InitThrows { get; set; }

    public Dictionary<string, EVROverlayError> CreateResults { get; } = [];

    public ConcurrentQueue<EVREventType> SystemEvents { get; } = new();

    public ConcurrentDictionary<ulong, ConcurrentQueue<EVREventType>> OverlayEvents { get; } = new();

    public HmdMatrix34_t? Head { get; set; } = new HmdMatrix34_t { m0 = 1, m5 = 1, m10 = 1 };

    public byte[]? LastRaw { get; private set; }

    public IReadOnlyList<string> Calls => [.. _calls.Select(c => c.Call)];

    public IReadOnlyList<int> Threads => [.. _calls.Select(c => c.Thread).Distinct()];

    public void Overlay(ulong handle, EVREventType type) => OverlayEvents.GetOrAdd(handle, _ => new()).Enqueue(type);

    public bool TryLoad()
    {
        Record("TryLoad");
        return Loadable;
    }

    public EVRInitError Init(EVRApplicationType type)
    {
        Record($"Init {type}");
        return InitThrows is { } error ? throw error : InitResult;
    }

    public void Shutdown() => Record("Shutdown");

    public EVROverlayError CreateOverlay(string key, string name, out ulong handle)
    {
        Record($"Create {key}");
        var result = CreateResults.GetValueOrDefault(key);
        handle = result == EVROverlayError.None ? Interlocked.Increment(ref _next) : 0;
        return result;
    }

    public EVROverlayError DestroyOverlay(ulong handle) => Recorded($"Destroy {handle}");

    public EVROverlayError SetWidth(ulong handle, float meters) => Recorded(string.Create(CultureInfo.InvariantCulture, $"Width {handle} {meters}"));

    public EVROverlayError SetAlpha(ulong handle, float alpha) => Recorded(string.Create(CultureInfo.InvariantCulture, $"Alpha {handle} {alpha}"));

    public EVROverlayError SetTransform(ulong handle, HmdMatrix34_t transform) => Recorded($"Transform {handle}");

    public EVROverlayError SetRaw(ulong handle, byte[] rgba, int width, int height)
    {
        LastRaw = rgba;
        return Recorded($"Raw {handle} {width}x{height}");
    }

    public EVROverlayError Show(ulong handle) => Recorded($"Show {handle}");

    public EVROverlayError Hide(ulong handle) => Recorded($"Hide {handle}");

    public EVREventType? PollSystem() => SystemEvents.TryDequeue(out var type) ? type : null;

    public EVREventType? PollOverlay(ulong handle) =>
        OverlayEvents.TryGetValue(handle, out var queue) && queue.TryDequeue(out var type) ? type : null;

    public HmdMatrix34_t? HeadPose()
    {
        Record("HeadPose");
        return Head;
    }

    public void AcknowledgeQuit() => Record("AcknowledgeQuit");

    private EVROverlayError Recorded(string call)
    {
        Record(call);
        return EVROverlayError.None;
    }

    private void Record(string call) => _calls.Enqueue((call, Environment.CurrentManagedThreadId));
}
