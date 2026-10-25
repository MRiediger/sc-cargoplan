using System.Runtime.CompilerServices;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public class LookPresetsTests
{
    [Fact]
    public void There_are_three_presets_default_first()
    {
        LookPresets.All.Select(p => p.Name).Should().Equal("Default", "High contrast", "Minimal");
    }

    [Fact]
    public void Default_is_the_look_without_settings()
    {
        // Drift guard: a new setting with a default changes the code, and this says the preset file must follow.
        LookPresets.All[0].Look.Should().Be(Look.Default);
    }

    [Fact]
    public void The_default_file_is_what_export_writes_for_the_defaults()
    {
        using MemoryStream written = new();
        LookFile.Write(written, Look.Default);

        Resource("default").Should().Equal(written.ToArray());
    }

    [Fact]
    public void Every_preset_is_complete_and_correct()
    {
        // Complete: read onto two different looks it gives the same, so no key is left to the look before.
        foreach (var preset in LookPresets.All)
        {
            var (onDefault, problems) = LookFile.Read(new MemoryStream(Resource(preset.FileName)), Look.Default);
            var (onCustom, _) = LookFile.Read(new MemoryStream(Resource(preset.FileName)), LookFileTests.Custom);

            onDefault.Should().Be(preset.Look, preset.Name);
            onCustom.Should().Be(preset.Look, preset.Name);
            problems.Should().BeEmpty(preset.Name);
        }
    }

    [Fact]
    public void Presets_differ()
    {
        LookPresets.All.Select(p => p.Look).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_preset_survives_the_settings_window_unchanged()
    {
        // The window keeps the hologram's opacity in whole percent: a preset at 0.555 would show as Custom at once.
        foreach (var preset in LookPresets.All)
        {
            (Math.Round(preset.Look.HoloOpacity * 100) / 100).Should().Be(preset.Look.HoloOpacity, preset.Name);
        }
    }

    [Fact]
    public void The_embedded_files_are_the_files_in_the_repository()
    {
        // A file restored by git keeps its old time stamp, and an incremental build may keep the old copy in the DLL.
        foreach (var preset in LookPresets.All)
        {
            Resource(preset.FileName).Should().Equal(File.ReadAllBytes(SourcePath(preset.FileName)), preset.Name);
        }
    }

    private static byte[] Resource(string fileName)
    {
        using var stream = LookPresets.Open(fileName);
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string SourcePath(string fileName, [CallerFilePath] string testFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", "src", "CargoPlan.App", "Looks", fileName + LookFile.Extension));
}
