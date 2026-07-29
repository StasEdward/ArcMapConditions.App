using System;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ArcMapConditions.App.Core;
using ArcMapConditions.App.Services;

namespace ArcMapConditions.App.ViewModels;

/// <summary>
/// View-model for one schedule row. Holds the parsed entry, recomputes its
/// displayed countdown text on every 1-second tick, and (for upcoming rows)
/// exposes a bell toggle to be reminded when the event starts.
/// </summary>
public sealed class ConditionRow : ObservableObject
{
    private readonly bool _isActive;
    private readonly MapConditionEntry _entry;
    private readonly SubscriptionManager _subs;
    private string _timeText = string.Empty;
    private bool _isSubscribed;

    public ConditionRow(MapConditionEntry entry, bool isActive, SubscriptionManager subs)
    {
        _entry = entry;
        _isActive = isActive;
        _subs = subs;

        Condition = entry.Condition;
        Map = entry.Map;
        Target = entry.Target;
        IconSlug = entry.IconSlug;
        Icon = IconProvider.Get(entry.IconSlug);

        _isSubscribed = subs.IsSubscribed(
            SubscriptionManager.KeyFor(entry.Condition, entry.Map, entry.Target));

        ToggleSubscribeCommand = new RelayCommand(ToggleSubscribe);

        Tick(DateTime.Now);
    }

    public string Condition { get; }

    public string Map { get; }

    public string IconSlug { get; }

    public DateTime Target { get; }

    public BitmapImage Icon { get; }

    public bool IsActive => _isActive;

    /// <summary>Only upcoming rows can be subscribed to (reminder at start).</summary>
    public bool CanSubscribe => !_isActive;

    public ICommand ToggleSubscribeCommand { get; }

    /// <summary>"Condition — Map", or just the condition when the map is unknown.</summary>
    public string Title =>
        string.IsNullOrEmpty(Map) ? Condition : $"{Condition}  •  {Map}";

    /// <summary>e.g. "ends in 4:37  (17:00)" / "starts in 1:37:02  (11:00)".</summary>
    public string TimeText
    {
        get => _timeText;
        private set => SetProperty(ref _timeText, value);
    }

    public bool IsSubscribed
    {
        get => _isSubscribed;
        private set => SetProperty(ref _isSubscribed, value);
    }

    public void Tick(DateTime now)
    {
        string label = _isActive ? "ends in" : "starts in";
        string remaining = CountdownFormat.FormatDuration(Target - now);
        TimeText = $"{label} {remaining}  ({Target:HH:mm})";
    }

    private void ToggleSubscribe() => IsSubscribed = _subs.Toggle(_entry);
}
