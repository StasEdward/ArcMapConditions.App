using System;
using System.Collections.Generic;
using System.Globalization;
using ArcMapConditions.App.Core;

namespace ArcMapConditions.App.Services;

/// <summary>
/// Tracks which upcoming events the user asked to be reminded about, and reports
/// which ones have just started. Keyed independently of the UI rows so a
/// subscription survives the 60-second list rebuilds.
/// </summary>
public sealed class SubscriptionManager : IDisposable
{
    private static readonly long BucketTicks = TimeSpan.FromMinutes(10).Ticks;

    private readonly Dictionary<string, Subscription> _subs = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>
    /// Stable key for one occurrence: condition + map + start time rounded to the
    /// nearest 10 minutes (events are hour-aligned, so this absorbs the few
    /// seconds of drift between refreshes while still separating occurrences).
    /// </summary>
    public static string KeyFor(string condition, string map, DateTime target)
    {
        long rounded = ((target.Ticks + BucketTicks / 2) / BucketTicks) * BucketTicks;
        var slot = new DateTime(rounded, target.Kind);
        return string.Concat(condition, "|", map, "|", slot.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
    }

    public bool IsSubscribed(string key) 
    { 
        lock (_lock)
        {
            return _subs.ContainsKey(key);
        }
    }

    /// <summary>Toggles the subscription for an entry. Returns the new state.</summary>
    public bool Toggle(MapConditionEntry entry)
    {
        lock (_lock)
        {
            string key = KeyFor(entry.Condition, entry.Map, entry.Target);
            if (_subs.Remove(key))
                return false;

            _subs[key] = new Subscription(key, entry.Condition, entry.Map, entry.IconSlug, entry.Target);
            return true;
        }
    }

    /// <summary>Re-syncs stored start times from the freshly parsed upcoming list.</summary>
    public void UpdateTargets(IEnumerable<MapConditionEntry> upcoming)
    {
        lock (_lock)
        {
            foreach (MapConditionEntry e in upcoming)
            {
                string key = KeyFor(e.Condition, e.Map, e.Target);
                if (_subs.TryGetValue(key, out Subscription? s) && !s.Notified)
                    s.Target = e.Target;
            }
        }
    }

    /// <summary>
    /// Returns subscriptions whose start time has arrived (and not yet fired),
    /// marking them fired and dropping them so each reminder shows once.
    /// </summary>
    public List<Subscription> CollectDue(DateTime now)
    {
        var due = new List<Subscription>();
        
        lock (_lock)
        {
            foreach (Subscription s in _subs.Values)
            {
                if (!s.Notified && now >= s.Target)
                {
                    s.Notified = true;
                    due.Add(s);
                }
            }

            foreach (Subscription s in due)
                _subs.Remove(s.Key);
        }
        
        return due;
    }

    public void Dispose()
    {
        // Nothing to dispose here
    }
}
