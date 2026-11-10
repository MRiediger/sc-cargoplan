using CargoPlan.App.Win32;
using CargoPlan.Ocr;

namespace CargoPlan.App.Scanning;

/// <summary>How a scan ended (T10.1b). Every case is a result; the notices for the player come with T10.3.</summary>
internal enum ScanAttemptOutcome
{
    /// <summary>A contract was read; <see cref="ScanAttempt.RawText"/> holds it.</summary>
    Read,

    /// <summary>The region holds no contract (no mobiGlas, another screen).</summary>
    NoContractInFrame,

    /// <summary>Star Citizen doesn't run, is minimised or has no client area.</summary>
    GameNotFound,

    /// <summary>GDI refused the copy (locked session, no desktop); <see cref="ScanAttempt.CaptureError"/> names why.</summary>
    CaptureFailed,

    /// <summary>The OCR didn't load (missing models, a broken runtime) or didn't start within the limit.</summary>
    Unavailable,

    /// <summary>Another scan was running; this one did nothing.</summary>
    Busy,
}

/// <summary>
/// One scan from the trigger to the raw text, with the time of each stage (Gate 1): waiting for the warm-up, finding the
/// window and capturing the region, then the OCR's own stages. <paramref name="Game"/> is the window that was read, for the
/// probe's log; null when none was found.
/// </summary>
internal sealed record ScanAttempt(
    ScanAttemptOutcome Outcome,
    string RawText,
    TimeSpan Wait,
    TimeSpan Capture,
    ScanTimings? Read,
    TimeSpan Total,
    GameWindow? Game = null,
    int CaptureError = 0);
