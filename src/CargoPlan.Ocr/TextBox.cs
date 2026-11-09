namespace CargoPlan.Ocr;

/// <summary>One block the OCR found: its text and the axis-aligned box around it, in pixels of the picture it read.</summary>
public sealed record TextBox(string Text, float Left, float Top, float Right, float Bottom)
{
    public float Height => Bottom - Top;

    public float Middle => (Top + Bottom) / 2;
}
