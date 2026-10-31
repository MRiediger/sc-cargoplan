using System.Text.Json;
using CargoPlan.Core.Json;
using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>Loads ship-outlines.json from the outlines folder next to the exe (D55), once at the start.</summary>
public static class ShipOutlineLoader
{
    public const int SupportedSchemaVersion = 1;

    /// <summary>The folder's name next to the exe, and in the repo root; it holds the file and its licence note.</summary>
    public const string FolderName = "outlines";

    public const string FileName = "ship-outlines.json";

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FolderName, FileName);

    /// <summary>
    /// Every valid entry of <paramref name="path"/> (default <see cref="DefaultPath"/>). Never throws for the file's
    /// sake: a file that can't be read gives no outlines and one problem, a broken entry is skipped with one.
    /// </summary>
    public static ShipOutlineCatalog Load(string? path = null)
    {
        path ??= DefaultPath;
        var name = Path.GetFileName(path);
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            SchemaVersion.Check(document.RootElement, SupportedSchemaVersion);
            if (!document.RootElement.TryGetProperty("ships", out var ships) || ships.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("\"ships\" must be a list.");
            }

            return Read(ships);
        }
        catch (JsonException ex)
        {
            return new ShipOutlineCatalog(ShipOutlineCatalog.Empty.Outlines, [$"{name} is invalid: {ex.Message}"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ShipOutlineCatalog(ShipOutlineCatalog.Empty.Outlines, [$"{name} can't be read: {ex.Message}"]);
        }
    }

    // One entry at a time from the document, so an entry without a slug or with a wrong type costs only itself.
    private static ShipOutlineCatalog Read(JsonElement ships)
    {
        Dictionary<string, ShipOutline> outlines = new(StringComparer.Ordinal);
        List<string> problems = [];
        var number = 0;
        foreach (var entry in ships.EnumerateArray())
        {
            number++;
            try
            {
                var dto = entry.Deserialize(ShipOutlineJsonContext.Default.ShipOutlineDto) ?? throw new JsonException("The entry is null.");
                var outline = ToOutline(dto);

                // A wrong height costs only the height: the outline then lies on the racks' floor.
                if (dto.HeightM is <= 0)
                {
                    problems.Add(Invariant($"Outline {number}: '{dto.Slug}': \"heightM\" must be positive, the outline is kept without it."));
                }

                if (!outlines.TryAdd(outline.Slug, outline))
                {
                    problems.Add(Invariant($"Outline {number}: '{outline.Slug}' is given more than once, the first one is kept."));
                }
            }
            catch (JsonException ex)
            {
                problems.Add(Invariant($"Outline {number}: {ex.Message}"));
            }
        }

        return new ShipOutlineCatalog(outlines, problems);
    }

    /// <exception cref="JsonException">The entry breaks a rule of the format.</exception>
    private static ShipOutline ToOutline(ShipOutlineDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Slug))
        {
            throw new JsonException("\"slug\" is empty.");
        }

        if (dto.Outline.Any(p => p.Count != 2))
        {
            throw new JsonException($"'{dto.Slug}': every point must be [x, y].");
        }

        OutlinePoint[] points = [.. dto.Outline.Select(p => new OutlinePoint(p[0], p[1]))];
        if (points.Length < 3)
        {
            throw new JsonException($"'{dto.Slug}' needs at least 3 points.");
        }

        // Shoelace formula: a polygon whose points lie on one line can't be drawn as an outline.
        var doubledArea = points.Zip(points.Skip(1).Append(points[0])).Sum(e => e.First.X * e.Second.Y - e.Second.X * e.First.Y);
        if (Math.Abs(doubledArea) < 1e-9)
        {
            throw new JsonException($"'{dto.Slug}' has no area, its points lie on one line.");
        }

        return new ShipOutline(dto.Slug, points, dto.HeightM is > 0 ? dto.HeightM : null);
    }
}
