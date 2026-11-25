namespace CargoPlan.App.Routing;

/// <summary>
/// How many loops the overlay's ship has measured (T13.6c, gate finding): the settings window shows it under the switch
/// "Use my measured loop times", so a switch that changes nothing yet says why. The Plan tab implements it; the window
/// knows only this, not the tab (E of the approval), and its tests use a fake.
/// </summary>
public interface IMeasuredSummary
{
    /// <summary><see cref="Text"/> is new; raised on the UI thread.</summary>
    event EventHandler? Changed;

    /// <summary>"2 loops measured with MISC Hull B", "No loop measured yet with … – finish a plan 3 times."; empty without a ship.</summary>
    string Text { get; }
}
