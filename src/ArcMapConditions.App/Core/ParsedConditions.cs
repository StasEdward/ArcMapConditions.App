using System;
using System.Collections.Generic;

namespace ArcMapConditions.App.Core;

/// <summary>Result of a single parse pass over the map-conditions page.</summary>
public sealed class ParsedConditions
{
    public ParsedConditions(
        IReadOnlyList<MapConditionEntry> active,
        IReadOnlyList<MapConditionEntry> upcoming,
        bool isValid)
    {
        Active = active;
        Upcoming = upcoming;
        IsValid = isValid;
    }

    public IReadOnlyList<MapConditionEntry> Active { get; }

    public IReadOnlyList<MapConditionEntry> Upcoming { get; }

    /// <summary>
    /// False when the page could not be parsed (markers missing / empty
    /// download). The caller keeps the previous good data in that case.
    /// </summary>
    public bool IsValid { get; }

    public static ParsedConditions Empty { get; } =
        new(Array.Empty<MapConditionEntry>(), Array.Empty<MapConditionEntry>(), isValid: false);
}
