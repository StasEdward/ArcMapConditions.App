using System;

namespace ArcMapConditions.App.Services;

/// <summary>A user's reminder request for one upcoming event occurrence.</summary>
public sealed class Subscription
{
    public Subscription(string key, string condition, string map, string iconSlug, DateTime target)
    {
        Key = key;
        Condition = condition;
        Map = map;
        IconSlug = iconSlug;
        Target = target;
    }

    public string Key { get; }

    public string Condition { get; }

    public string Map { get; }

    public string IconSlug { get; }

    /// <summary>Scheduled start; refreshed as the page re-syncs.</summary>
    public DateTime Target { get; set; }

    /// <summary>Set once the reminder has fired, so it never fires twice.</summary>
    public bool Notified { get; set; }
}
