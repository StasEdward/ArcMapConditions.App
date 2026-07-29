using System;

namespace ArcMapConditions.App.Core;

/// <summary>
/// A single parsed schedule entry (one map condition occurrence), either
/// currently active or upcoming. Mirrors the per-entry table produced by the
/// original Rainmeter Parser.lua.
/// </summary>
public sealed class MapConditionEntry
{
    /// <summary>Human-readable condition name, e.g. "Lush Blooms".</summary>
    public string Condition { get; init; } = string.Empty;

    /// <summary>Map name, e.g. "Dam Battlegrounds". May be empty if unknown.</summary>
    public string Map { get; init; } = string.Empty;

    /// <summary>Icon slug (file name without extension) in Assets/icons.</summary>
    public string IconSlug { get; init; } = "generic";

    /// <summary>
    /// Absolute local wall-clock instant the countdown runs to. For active
    /// entries this is the END of the event; for upcoming entries, the START.
    /// Computed as "now + parsed countdown seconds", so it carries no timezone
    /// assumptions (the source page renders the countdown token itself).
    /// </summary>
    public DateTime Target { get; init; }
}
