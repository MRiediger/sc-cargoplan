using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace CargoPlan.App.Tests;

/// <summary>WPF elements need a single-threaded apartment; xUnit's threads aren't. For new tests (Phase 8).</summary>
public static class Sta
{
    /// <summary>
    /// The test process renders in software only (B11, D98): with a GPU, WPF's render thread creates a D3D9 device for a
    /// shown window or popup while another test thread renders a Viewport3D into a RenderTargetBitmap through D3D9 too,
    /// and the two take d3d9's locks in opposite order (dump of 9 Nov 2026). CI has no GPU and always rendered like this.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.renderoptions.processrendermode
    /// </summary>
    // CA2255 warns libraries off module initializers; a test assembly is never shipped, which the rule allows (see TestLog).
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void RenderInSoftware() => RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
#pragma warning restore CA2255

    public static void Run(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
