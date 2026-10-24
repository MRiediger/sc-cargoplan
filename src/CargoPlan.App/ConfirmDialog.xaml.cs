using System.Windows;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;

namespace CargoPlan.App;

/// <summary>
/// The app's yes/no question (ship change D59, re-plan D66), centred on the window the action came from instead of the
/// monitor like a message box. Esc and the close button answer no; Enter answers what <c>yesIsDefault</c> says.
/// </summary>
public partial class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string question, bool yesIsDefault, ToolWindowTheme theme, Window owner)
    {
        // Before InitializeComponent, like every tool window (D36).
        ToolWindowThemer.Apply(this, theme);
        InitializeComponent();
        Title = title;
        Question.Text = question;
        Owner = owner;
        YesButton.IsDefault = yesIsDefault;
        NoButton.IsDefault = !yesIsDefault;
        Loaded += (_, _) => (yesIsDefault ? YesButton : NoButton).Focus();
    }

    /// <summary>Asks modally, centred on <paramref name="owner"/>, which must be shown; true only for Yes.</summary>
    public static bool Ask(Window owner, string title, string question, bool yesIsDefault, ToolWindowTheme theme) =>
        new ConfirmDialog(title, question, yesIsDefault, theme, owner).ShowDialog() == true;

    // IsCancel closes with DialogResult false by itself; Yes has to set it.
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.controls.button.iscancel
    private void OnYes(object sender, RoutedEventArgs e) => DialogResult = true;
}
