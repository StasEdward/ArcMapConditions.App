using System;
using System.Linq;
using ArcMapConditions.App.Core;

// Smoke test for ConditionsParser: feeds a synthetic HTML sample that mirrors
// the real arcraiders.com/map-conditions structure (leading countdown token,
// concatenated condition+map spans, "Mon D · h:mm AM - h:mm AM" UTC range,
// anchor href = /map-conditions/{slug}) and asserts the extracted entries.

static string Entry(string slug, string countdown, string condition, string map, string range) =>
    $"<a class=\"card\" href=\"https://arcraiders.com/map-conditions/{slug}\">" +
    $"<span class=\"cd\">{countdown}</span>" +
    $"<span class=\"name\">{condition}</span><span class=\"map\">{map}</span>" +
    $"<span class=\"range\">{range}</span></a>";

string html =
    "<html><body>" +
    "<nav><a href=\"https://arcraiders.com/map-conditions\">Map Conditions</a></nav>" +
    "<section><h2>Active now</h2>" +
    Entry("hurricane", "37:02", "Hurricane", "Dam Battlegrounds", "Jul 29 · 9:00 AM - 10:00 AM") +
    "</section>" +
    "<section><h2>Coming up</h2>" +
    Entry("harvester", "37:02", "Harvester", "Dam Battlegrounds", "Jul 29 · 10:00 AM - 11:00 AM") +
    Entry("matriarch", "37:02", "Matriarch", "The Blue Gate", "Jul 29 · 10:00 AM - 11:00 AM") +
    Entry("night-raid", "37:02", "Night Raid", "Spaceport", "Jul 29 · 10:00 AM - 11:00 AM") +
    Entry("electromagnetic-storm", "1:37:02", "Electromagnetic Storm", "The Blue Gate", "Jul 29 · 11:00 AM - 12:00 PM") +
    Entry("lush-blooms", "2:05:00", "Lush Blooms", "Riven Tides", "Jul 29 · 12:00 PM - 1:00 PM") +
    Entry("some-future-condition", "7:37:02", "Some Future Condition", "Buried City", "Jul 29 · 5:00 PM - 6:00 PM") +
    "</section>" +
    "</body></html>";

var now = new DateTime(2026, 7, 29, 9, 23, 0, DateTimeKind.Local);
ParsedConditions result = ConditionsParser.Parse(html, now);

int failures = 0;
void Check(string label, bool ok, string detail = "")
{
    Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + (detail.Length > 0 ? "  -> " + detail : ""));
    if (!ok) failures++;
}

Console.WriteLine("== ConditionsParser smoke test ==");
Check("parse is valid", result.IsValid);
Check("1 active entry", result.Active.Count == 1, $"got {result.Active.Count}");
Check("6 upcoming entries", result.Upcoming.Count == 6, $"got {result.Upcoming.Count}");

if (result.Active.Count == 1)
{
    var a = result.Active[0];
    Check("active condition = Hurricane", a.Condition == "Hurricane", a.Condition);
    Check("active map = Dam Battlegrounds", a.Map == "Dam Battlegrounds", a.Map);
    Check("active icon = hurricane", a.IconSlug == "hurricane", a.IconSlug);
    // 37:02 countdown from 09:23:00 -> target 10:00:02
    Check("active target = 10:00:02", a.Target == now.AddSeconds(37 * 60 + 2), a.Target.ToString("HH:mm:ss"));
}

var byCond = result.Upcoming.ToDictionary(e => e.Condition, e => e);
Check("upcoming has Night Raid (multi-word slug)", byCond.ContainsKey("Night Raid"));
if (byCond.TryGetValue("Night Raid", out var nr))
    Check("Night Raid map = Spaceport", nr.Map == "Spaceport", nr.Map);

Check("upcoming has Lush Blooms", byCond.ContainsKey("Lush Blooms"));
if (byCond.TryGetValue("Lush Blooms", out var lb))
{
    Check("Lush Blooms map = Riven Tides", lb.Map == "Riven Tides", lb.Map);
    // 2:05:00 = H:MM:SS = 2h5m
    Check("Lush Blooms target = +2:05:00", lb.Target == now.AddSeconds(2 * 3600 + 5 * 60), lb.Target.ToString("HH:mm:ss"));
}

// Unknown condition -> generic icon, but name still derived from slug.
Check("unknown condition present", byCond.ContainsKey("Some Future Condition"));
if (byCond.TryGetValue("Some Future Condition", out var sf))
{
    Check("unknown condition uses generic icon", sf.IconSlug == "generic", sf.IconSlug);
    Check("unknown condition map = Buried City", sf.Map == "Buried City", sf.Map);
}

Check("upcoming sorted by target ascending",
    result.Upcoming.Select(e => e.Target).SequenceEqual(result.Upcoming.Select(e => e.Target).OrderBy(t => t)));

// Guard: a bad page (no markers) yields an invalid result so the UI keeps old data.
var bad = ConditionsParser.Parse("<html>totally different page</html>", now);
Check("invalid page -> IsValid false", !bad.IsValid);

// ---- Subscription manager -------------------------------------------------
Console.WriteLine();
Console.WriteLine("== SubscriptionManager ==");
var subs = new ArcMapConditions.App.Services.SubscriptionManager();
var upcomingEntry = new MapConditionEntry
{
    Condition = "Harvester",
    Map = "Dam Battlegrounds",
    IconSlug = "harvester",
    Target = now.AddMinutes(37),
};

string key = ArcMapConditions.App.Services.SubscriptionManager.KeyFor(
    upcomingEntry.Condition, upcomingEntry.Map, upcomingEntry.Target);

Check("not subscribed initially", !subs.IsSubscribed(key));
Check("toggle on returns true", subs.Toggle(upcomingEntry) == true);
Check("now subscribed", subs.IsSubscribed(key));
Check("nothing due before start", subs.CollectDue(now).Count == 0);

// Key is stable against a few seconds of refresh drift (rounds to 10 min).
var drifted = new MapConditionEntry
{
    Condition = "Harvester", Map = "Dam Battlegrounds", IconSlug = "harvester",
    Target = upcomingEntry.Target.AddSeconds(4),
};
Check("key stable across small drift",
    ArcMapConditions.App.Services.SubscriptionManager.KeyFor(drifted.Condition, drifted.Map, drifted.Target) == key);

// After the start time passes, exactly one reminder is due, then never again.
var afterStart = upcomingEntry.Target.AddSeconds(1);
var due1 = subs.CollectDue(afterStart);
Check("one reminder fires at start", due1.Count == 1 && due1[0].Condition == "Harvester");
Check("no longer subscribed after firing", !subs.IsSubscribed(key));
Check("does not fire twice", subs.CollectDue(afterStart.AddMinutes(1)).Count == 0);

// Toggling off removes it.
subs.Toggle(upcomingEntry);
Check("toggle on again", subs.IsSubscribed(key));
Check("toggle off returns false", subs.Toggle(upcomingEntry) == false);
Check("removed after toggle off", !subs.IsSubscribed(key));

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} CHECK(S) FAILED");
Environment.Exit(failures == 0 ? 0 : 1);
