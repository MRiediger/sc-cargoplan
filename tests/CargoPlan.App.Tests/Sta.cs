using System.Runtime.ExceptionServices;

namespace CargoPlan.App.Tests;

/// <summary>WPF elements need a single-threaded apartment; xUnit's threads aren't. For new tests (Phase 8).</summary>
public static class Sta
{
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
