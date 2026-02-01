using OBSReplay.Host.Models;

namespace OBSReplay.Host.Services;

/// <summary>
/// Converts between OBS linear volume multipliers and perceptual fader positions (0-100).
/// Uses the same cubic-root mapping as OBS Studio's audio mixer.
/// </summary>
public static class AudioMathService
{
    private const double LogRangeDb = Constants.FaderLogRangeDb;   // -96.0
    private const double LogOffsetDb = Constants.FaderLogOffsetDb; // +6.0
    private const double TotalRangeDb = -(LogRangeDb - LogOffsetDb); // 102.0

    /// <summary>
    /// Convert OBS linear volume multiplier to fader percentage (0-100).
    /// </summary>
    public static int MulToFader(double mul)
    {
        if (mul <= 0.0)
            return 0;

        double db = 20.0 * Math.Log10(mul);

        if (db < LogRangeDb)
            return 0;
        if (db > LogOffsetDb)
            return 100;

        // Normalize to 0..1 range
        double normalized = (db - LogRangeDb) / TotalRangeDb;

        // Apply cubic root for perceptual linearity
        double fader = Math.Pow(normalized, 1.0 / 3.0);

        return (int)Math.Round(fader * 100.0);
    }

    /// <summary>
    /// Convert fader percentage (0-100) to OBS linear volume multiplier.
    /// </summary>
    public static double FaderToMul(int faderPct)
    {
        if (faderPct <= 0)
            return 0.0;
        if (faderPct >= 100)
            return Math.Pow(10.0, LogOffsetDb / 20.0);

        double fader = faderPct / 100.0;

        // Reverse cubic root: cube the value
        double normalized = fader * fader * fader;

        // Convert back to dB
        double db = normalized * TotalRangeDb + LogRangeDb;

        // Convert dB to linear multiplier
        return Math.Pow(10.0, db / 20.0);
    }
}
