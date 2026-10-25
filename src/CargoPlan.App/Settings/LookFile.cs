using System.IO;
using System.Text.Json;

namespace CargoPlan.App.Settings;

/// <summary>
/// Reads and writes a look file, <c>*.cargoplan-look.json</c> (K4.3, D77): the keys of <see cref="Look"/> in the shape and
/// with the names of settings.json, so settings-format.md explains both. The file boundary: only here are JSON errors
/// expected.
/// </summary>
public static class LookFile
{
    public const int SchemaVersion = 1;

    public const string Extension = ".cargoplan-look.json";

    /// <summary>
    /// Reads a look file onto <paramref name="current"/>: a look key or an appearance section the file leaves out stays as
    /// it is; inside a section the file has, a missing value takes its default, as settings.json reads it (D31). Values
    /// are checked like settings.json's (<see cref="AppearanceRules.Normalize(Look)"/>).
    /// </summary>
    /// <returns>The look, and one line per corrected value.</returns>
    /// <exception cref="JsonException">
    /// Not JSON, no numeric schemaVersion, another schema, an unknown key (cameras and hotkeys included) or a wrong type;
    /// the schema is checked first, so a newer file is named by its version rather than by a key it brings.
    /// </exception>
    public static (Look Look, IReadOnlyList<string> Problems) Read(Stream stream, Look current)
    {
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        // The wording of Core's SchemaVersion (internal there), so every file of the app names a version alike.
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var number))
        {
            throw new JsonException("Missing numeric \"schemaVersion\".");
        }

        if (number != SchemaVersion)
        {
            throw new JsonException($"schemaVersion {number} is not supported (expected {SchemaVersion}).");
        }

        var content = root.Deserialize(SettingsJsonContext.Default.LookFileContent) ?? throw new JsonException("The file contains null.");
        var appearance = content.Appearance;
        Look merged = new(
            new AppearanceSettings(
                appearance?.Window ?? current.Appearance.Window,
                appearance?.Header ?? current.Appearance.Header,
                appearance?.Legend ?? current.Appearance.Legend,
                appearance?.Crates ?? current.Appearance.Crates,
                appearance?.Buttons ?? current.Appearance.Buttons),
            content.Legend ?? current.Legend,
            content.ToolWindowTheme ?? current.ToolWindowTheme,
            content.ShipOutline ?? current.ShipOutline,
            content.HoloOpacity ?? current.HoloOpacity,
            content.HoloHullColor ?? current.HoloHullColor,
            content.HoloEdgeColor ?? current.HoloEdgeColor,
            content.ShipView?.MaxZoomPxPerCell ?? current.MaxZoomPxPerCell);
        return AppearanceRules.Normalize(merged);
    }

    /// <summary>Writes every key of the look, so a file read onto any look gives this one.</summary>
    public static void Write(Stream stream, Look look) =>
        JsonSerializer.Serialize(
            stream,
            new LookFileContent(
                SchemaVersion,
                look.Legend,
                look.ShipOutline,
                look.HoloOpacity,
                look.HoloHullColor,
                look.HoloEdgeColor,
                new LookShipView(look.MaxZoomPxPerCell),
                look.ToolWindowTheme,
                new LookAppearance(look.Appearance.Window, look.Appearance.Header, look.Appearance.Legend, look.Appearance.Crates, look.Appearance.Buttons)),
            SettingsJsonContext.Default.LookFileContent);
}

/// <summary>
/// A look file as JSON, the keys in settings.json's order. Every key nullable, so "missing" can mean "unchanged"; the
/// sections are settings.json's records, so a section the file has reads exactly as there.
/// </summary>
internal sealed record LookFileContent(
    int SchemaVersion,
    LegendSettings? Legend = null,
    string? ShipOutline = null,
    double? HoloOpacity = null,
    string? HoloHullColor = null,
    string? HoloEdgeColor = null,
    LookShipView? ShipView = null,
    string? ToolWindowTheme = null,
    LookAppearance? Appearance = null);

/// <summary>Like <see cref="AppearanceSettings"/>, but a missing section stays null instead of taking its defaults.</summary>
internal sealed record LookAppearance(
    WindowAppearance? Window = null,
    HeaderAppearance? Header = null,
    LegendAppearance? Legend = null,
    CrateAppearance? Crates = null,
    ButtonAppearance? Buttons = null);

/// <summary>The look's part of <c>shipView</c>: the max zoom; <c>cameras</c> here is an unknown key.</summary>
internal sealed record LookShipView(double MaxZoomPxPerCell = AppearanceRules.DefaultMaxZoom);
