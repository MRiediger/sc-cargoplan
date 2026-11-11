using CargoPlan.App.Hotkeys;

namespace CargoPlan.App.Tests;

/// <summary>
/// ContractTracker reacts to F3 too (S10.0 K2): with a polled hotkey nobody holds a key, both programs act (D104). The
/// notice says so once per run, while it is true.
/// </summary>
public sealed class ScanKeyCheckTests
{
    private bool _running;

    private int _asked;

    private ScanKeyCheck Create() => new(() =>
    {
        _asked++;
        return _running;
    });

    private static Hotkey Key(string text)
    {
        HotkeyParser.TryParse(text, out var hotkey, out _).Should().BeTrue();
        return hotkey!;
    }

    [Fact]
    public void F3_with_ContractTracker_running_gives_the_notice()
    {
        _running = true;

        Create().Check(Key("F3")).Should().Be(ScanKeyCheck.Text);
    }

    [Fact]
    public void The_notice_names_ContractTracker_and_the_way_out()
    {
        ScanKeyCheck.Text.Should().Be("ContractTracker also reacts to F3 – close it or change the scan hotkey.");
    }

    [Fact]
    public void Without_ContractTracker_there_is_none()
    {
        Create().Check(Key("F3")).Should().BeNull();
    }

    [Theory]
    [InlineData("F5")]
    [InlineData("Ctrl+F3")]
    public void Another_scan_key_needs_no_look_at_the_processes(string key)
    {
        _running = true;

        Create().Check(Key(key)).Should().BeNull();
        _asked.Should().Be(0);
    }

    [Fact]
    public void No_scan_key_gives_none()
    {
        _running = true;

        Create().Check(null).Should().BeNull();
    }

    [Fact]
    public void It_stays_while_ContractTracker_runs_and_goes_when_it_is_closed()
    {
        _running = true;
        var check = Create();
        check.Check(Key("F3"));

        check.Check(Key("F3")).Should().Be(ScanKeyCheck.Text);
        _running = false;
        check.Check(Key("F3")).Should().BeNull();
    }

    [Fact]
    public void Once_per_run_it_doesnt_come_back()
    {
        _running = true;
        var check = Create();
        check.Check(Key("F3"));
        _running = false;
        check.Check(Key("F3"));

        _running = true;
        check.Check(Key("F3")).Should().BeNull();
    }

    [Fact]
    public void Another_scan_key_ends_it()
    {
        _running = true;
        var check = Create();
        check.Check(Key("F3"));

        check.Check(Key("F5")).Should().BeNull();
        check.Check(Key("F3")).Should().BeNull("once per run");
    }
}
