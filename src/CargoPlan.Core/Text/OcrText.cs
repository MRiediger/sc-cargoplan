using System.Text;

namespace CargoPlan.Core.Text;

/// <summary>
/// Text as OCR may read it, in one place for the importer's headings and names and for Core's names of one place (B1,
/// <see cref="DestinationNames"/>). Moved from the importer with T13.3, so the route planner compares scanned names
/// with the data's names the same way.
/// </summary>
public static class OcrText
{
    /// <summary>
    /// Text as OCR may read it: no spaces, upper case, O and 0 alike, I, L, 1 and | alike ("DETAlLS", "ACCEPT 0FFER",
    /// "Melodic Fieids"). Applied to both sides of a comparison: headings, name completion, names of one place (B1).
    /// </summary>
    public static string AnchorForm(string text)
    {
        var form = new StringBuilder(text.Length);
        foreach (var c in text.Where(c => !char.IsWhiteSpace(c)))
        {
            form.Append(char.ToUpperInvariant(c) switch
            {
                '0' => 'O',
                'L' or '1' or '|' => 'I',
                var upper => upper,
            });
        }

        return form.ToString();
    }
}
