using System;

namespace ArcMapConditions.App.Core;

/// <summary>Formatting helpers shared by the UI (ported from Parser.lua).</summary>
public static class CountdownFormat
{
    /// <summary>
    /// Formats a duration as "H:MM:SS" (when >= 1h) or "M:SS" otherwise.
    /// Negative or null values clamp to zero.
    /// </summary>
    public static string FormatDuration(TimeSpan span)
    {
        long sec = (long)Math.Floor(span.TotalSeconds);
        if (sec < 0) sec = 0;

        long h = sec / 3600;
        long m = (sec % 3600) / 60;
        long s = sec % 60;

        return h > 0
            ? $"{h}:{m:D2}:{s:D2}"
            : $"{m}:{s:D2}";
    }
}
