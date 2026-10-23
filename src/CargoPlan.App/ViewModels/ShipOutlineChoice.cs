using CargoPlan.App.Settings;

namespace CargoPlan.App.ViewModels;

/// <summary>One entry of the settings window's ship outline list (D62): the value for the session, the text for the player.</summary>
public sealed record ShipOutlineChoice(ShipOutlineMode Value, string Text);
