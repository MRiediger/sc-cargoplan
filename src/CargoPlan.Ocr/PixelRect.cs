namespace CargoPlan.Ocr;

/// <summary>A rectangle in image pixels: left, top, width, height.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
