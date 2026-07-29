using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ArcMapConditions.App.Core;

/// <summary>
/// Parses the raw HTML of https://arcraiders.com/map-conditions into the
/// "Active now" and "Coming up" lists.
///
/// This is a faithful C# port of @Resources/Lua/Parser.lua from the Rainmeter
/// skin. Design notes carried over from the original:
///
///  * NO TIMEZONE MATH for the countdowns. The site server-renders a ready-made
///    countdown token at the start of every entry ("16:06", "1:16:06",
///    "14:16:06"). We parse that token, convert it to seconds and pin it to the
///    local clock at the moment of download; the UI ticks it down every second.
///    Every ~60s the page is re-downloaded and the targets are re-synced, so
///    drift never exceeds seconds.
///  * The condition name is derived from the URL slug
///    (/map-conditions/lush-blooms -> "Lush Blooms"), so any current or future
///    condition is picked up automatically. Unknown conditions fall back to the
///    generic icon.
///  * If the countdown token is ever missing from the markup, a fallback parses
///    the printed date range AS UTC (matching the site) and compares it against
///    the UTC clock - still no local-time mixing.
/// </summary>
public static class ConditionsParser
{
    // Maps that exist in the game; used only as a fallback to split the
    // "ConditionMap" text when the slug-derived name doesn't match.
    private static readonly string[] Maps =
    {
        "Dam Battlegrounds", "The Blue Gate", "Stella Montis",
        "Buried City", "Riven Tides", "Spaceport",
    };

    // Icon files present in Assets/icons (file name = slug). Anything not
    // listed here renders with generic.png.
    private static readonly HashSet<string> KnownIcons = new(StringComparer.Ordinal)
    {
        "beachcombing", "bird-city", "close-scrutiny", "electromagnetic-storm",
        "harvester", "hidden-bunker", "hurricane", "husk-graveyard",
        "launch-tower-loot", "locked-gate", "lush-blooms", "matriarch",
        "night-raid", "prospecting-probes", "uncovered-caches",
    };

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jan"] = 1, ["Feb"] = 2, ["Mar"] = 3, ["Apr"] = 4, ["May"] = 5, ["Jun"] = 6,
        ["Jul"] = 7, ["Aug"] = 8, ["Sep"] = 9, ["Oct"] = 10, ["Nov"] = 11, ["Dec"] = 12,
    };

    // Each schedule card is an <a href=".../map-conditions/{slug}">...</a>.
    private static readonly Regex EntryRegex = new(
        "<a[^>]*?href=\"[^\"]*?/map-conditions/([a-zA-Z-]+)\"[^>]*>(.*?)</a>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Leading server-rendered countdown token: M:SS, H:MM:SS or D:HH:MM:SS.
    private static readonly Regex CountdownRegex = new(
        @"^\s*\d[\d:]*\d", RegexOptions.Compiled);

    // Printed schedule range, e.g. "Jul 29 · 9:00 AM - 10:00 AM" (UTC).
    private static readonly Regex DateRangeRegex = new(
        @"([A-Za-z]{3})\s+(\d+)\D*?(\d+):(\d+)\s*([A-Za-z]{2})\s*-\s*(\d+):(\d+)\s*([A-Za-z]{2})",
        RegexOptions.Compiled);

    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex DigitsRegex = new(@"\d+", RegexOptions.Compiled);

    /// <summary>
    /// Parses the page HTML. Returns null lists when the expected
    /// "Active now" / "Coming up" section markers are not found (e.g. the page
    /// failed to load), so the caller can keep showing the last good data.
    /// </summary>
    public static ParsedConditions Parse(string? html, DateTime nowLocal)
    {
        if (string.IsNullOrEmpty(html))
            return ParsedConditions.Empty;

        int activeStart = html.IndexOf("Active now", StringComparison.OrdinalIgnoreCase);
        int upcomingStart = html.IndexOf("Coming up", StringComparison.OrdinalIgnoreCase);
        if (activeStart < 0 || upcomingStart < 0 || upcomingStart <= activeStart)
            return ParsedConditions.Empty;

        long nowUtcEpoch = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

        var active = ExtractEntries(
            html.Substring(activeStart, upcomingStart - activeStart), Mode.End, nowLocal, nowUtcEpoch);
        var upcoming = ExtractEntries(
            html.Substring(upcomingStart), Mode.Start, nowLocal, nowUtcEpoch);

        return new ParsedConditions(active, upcoming, isValid: true);
    }

    private enum Mode { End, Start }

    private static List<MapConditionEntry> ExtractEntries(
        string section, Mode mode, DateTime nowLocal, long nowUtcEpoch)
    {
        var entries = new List<MapConditionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in EntryRegex.Matches(section))
        {
            string slug = m.Groups[1].Value;
            string text = StripTags(m.Groups[2].Value);

            (int? secs, string rest) = ParseCountdown(text);

            Match range = DateRangeRegex.Match(rest);

            // Fallback: no countdown token -> compute remaining seconds from the
            // printed range, treating it as UTC and comparing with the UTC clock.
            if (secs is null && range.Success)
            {
                if (Months.TryGetValue(range.Groups[1].Value, out int mn))
                {
                    int day = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
                    int hh, mi;
                    if (mode == Mode.End)
                    {
                        hh = To24H(range.Groups[6].Value, range.Groups[8].Value);
                        mi = int.Parse(range.Groups[7].Value, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        hh = To24H(range.Groups[3].Value, range.Groups[5].Value);
                        mi = int.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture);
                    }

                    int year = DateTime.UtcNow.Year;
                    long ts = ToEpochSeconds(year, mn, day, hh, mi);
                    // Roll into next year if the date is far in the past (year wrap).
                    if (ts < nowUtcEpoch - 200L * 86400L)
                        ts = ToEpochSeconds(year + 1, mn, day, hh, mi);
                    secs = (int)(ts - nowUtcEpoch);
                }
            }

            // Accept only entries that look like schedule cards (nav links have
            // neither a countdown token nor a date range).
            if (secs is int s && s > -120)
            {
                string head = range.Success ? rest.Substring(0, range.Index) : rest;
                string slugName = SlugToName(slug);
                (string condition, string mapName) = SplitConditionMap(head, slugName);

                string keyTail = range.Success
                    ? rest.Substring(range.Index, Math.Min(31, rest.Length - range.Index))
                    : s.ToString(CultureInfo.InvariantCulture);
                string key = slug + "|" + (string.IsNullOrEmpty(mapName) ? "?" : mapName) + "|" + keyTail;

                if (seen.Add(key))
                {
                    entries.Add(new MapConditionEntry
                    {
                        Condition = condition,
                        Map = mapName,
                        IconSlug = KnownIcons.Contains(slug) ? slug : "generic",
                        Target = nowLocal.AddSeconds(s),
                    });
                }
            }
        }

        entries.Sort((a, b) => a.Target.CompareTo(b.Target));
        return entries;
    }

    /// <summary>
    /// Parses the leading countdown token of an entry's text.
    /// Returns (seconds, restOfText) or (null, text) when there is no token.
    /// Accepts M:SS, H:MM:SS and D:HH:MM:SS.
    /// </summary>
    private static (int? seconds, string rest) ParseCountdown(string text)
    {
        Match m = CountdownRegex.Match(text);
        if (!m.Success)
            return (null, text);

        string token = m.Value.TrimStart();
        if (!token.Contains(':'))
            return (null, text);

        var parts = new List<int>();
        foreach (Match d in DigitsRegex.Matches(token))
            parts.Add(int.Parse(d.Value, CultureInfo.InvariantCulture));

        int? secs = parts.Count switch
        {
            2 => parts[0] * 60 + parts[1],
            3 => parts[0] * 3600 + parts[1] * 60 + parts[2],
            4 => parts[0] * 86400 + parts[1] * 3600 + parts[2] * 60 + parts[3],
            _ => null,
        };

        if (secs is null)
            return (null, text);

        return (secs, text.Substring(m.Index + m.Length));
    }

    /// <summary>
    /// Splits "Husk GraveyardRiven Tides" (tag stripping concatenates the
    /// condition and map spans) using the slug-derived condition name first,
    /// then falling back to the known map list.
    /// </summary>
    private static (string condition, string map) SplitConditionMap(string head, string slugName)
    {
        head = head.Trim();

        if (head.Length >= slugName.Length &&
            head.Substring(0, slugName.Length).Equals(slugName, StringComparison.OrdinalIgnoreCase))
        {
            string mapName = head.Substring(slugName.Length).TrimStart();
            if (mapName.Length > 0)
                return (slugName, mapName);
        }

        foreach (string mp in Maps)
        {
            if (head.Length > mp.Length && head.EndsWith(mp, StringComparison.Ordinal))
                return (head.Substring(0, head.Length - mp.Length).TrimEnd(), mp);
        }

        return (slugName, head);
    }

    /// <summary>"lush-blooms" -> "Lush Blooms".</summary>
    private static string SlugToName(string slug)
    {
        string spaced = slug.Replace('-', ' ');
        return Regex.Replace(spaced, @"[A-Za-z][A-Za-z']*", match =>
            char.ToUpperInvariant(match.Value[0]) + match.Value.Substring(1));
    }

    private static string StripTags(string s)
    {
        string noTags = TagRegex.Replace(s, string.Empty);
        // Decode HTML character references (equivalent to Rainmeter's
        // DecodeCharacterReference=1), e.g. "&amp;" -> "&", "&#183;" -> "·".
        noTags = System.Net.WebUtility.HtmlDecode(noTags);
        return WhitespaceRegex.Replace(noTags, " ").Trim();
    }

    private static int To24H(string hour, string ampm)
    {
        int h = int.Parse(hour, CultureInfo.InvariantCulture);
        if (ampm.Equals("AM", StringComparison.OrdinalIgnoreCase))
        {
            if (h == 12) h = 0;
        }
        else
        {
            if (h != 12) h += 12;
        }
        return h;
    }

    // --- UTC fallback date math (Howard Hinnant's days_from_civil) ----------

    private static long DaysFromCivil(int y, int m, int d)
    {
        if (m <= 2) y -= 1;
        int era = (y >= 0 ? y : y - 399) / 400;
        int yoe = y - era * 400;
        int mp = (m + 9) % 12;
        int doy = (153 * mp + 2) / 5 + d - 1;
        int doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        return era * 146097L + doe - 719468L;
    }

    private static long ToEpochSeconds(int y, int m, int d, int hour, int min)
        => DaysFromCivil(y, m, d) * 86400L + hour * 3600L + min * 60L;
}
