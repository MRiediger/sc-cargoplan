using Velopack;

namespace CargoPlan.App;

/// <summary>
/// The entry point (D109). Velopack's WPF sample puts <c>Main</c> into <c>App</c> and names <c>App</c> as the startup object;
/// next to the <c>Main</c> WPF generates for App.xaml that fails with CS0017 (S11.0), so ours has a class of its own,
/// named as <c>StartupObject</c> in the csproj.
/// Source: https://github.com/velopack/velopack/blob/1.2.161/samples/CSharpWpf/App.xaml.cs
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // The first line (CLAUDE.md A14): started with --veloapp-* by an install or update, Velopack runs its hook and ends
        // the process in here; code above would run each time. Auto-apply off: we apply right after the download (T11.2),
        // so a package left in packages\ means a failed apply (UAC declined, folder not writable), and swapping it silently
        // with a restart, or asking for UAC, at every start is worse than offering it again (D109).
        // Source: Velopack.xml 1.2.161, VelopackApp.SetAutoApplyOnStartup ("This is ON by default").
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
