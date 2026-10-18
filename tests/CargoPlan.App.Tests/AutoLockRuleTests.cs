namespace CargoPlan.App.Tests;

public class AutoLockRuleTests
{
    [Fact]
    public void Locks_when_Star_Citizen_is_in_front_and_the_cursor_is_hidden()
    {
        AutoLockRule.ShouldLock(AutoLockRule.GameProcessName, cursorShowing: false).Should().BeTrue();
    }

    [Fact]
    public void Stays_unlocked_when_Star_Citizen_shows_the_cursor()
    {
        // mobiGlas, Esc menu, freight elevator terminal: the player needs the overlay.
        AutoLockRule.ShouldLock(AutoLockRule.GameProcessName, cursorShowing: true).Should().BeFalse();
    }

    [Fact]
    public void Stays_unlocked_when_another_program_hides_the_cursor()
    {
        // Windows Terminal hides the cursor while typing (spike, D11 addendum T3.3).
        AutoLockRule.ShouldLock("WindowsTerminal", cursorShowing: false).Should().BeFalse();
    }

    [Fact]
    public void Stays_unlocked_without_a_foreground_process()
    {
        AutoLockRule.ShouldLock(null, cursorShowing: false).Should().BeFalse();
    }

    [Theory]
    [InlineData("starcitizen")]
    [InlineData("STARCITIZEN")]
    public void Compares_the_process_name_case_insensitively(string processName)
    {
        AutoLockRule.ShouldLock(processName, cursorShowing: false).Should().BeTrue();
    }
}
