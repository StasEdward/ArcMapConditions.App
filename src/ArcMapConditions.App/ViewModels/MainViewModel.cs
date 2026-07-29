using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ArcMapConditions.App.Core;
using ArcMapConditions.App.Services;

namespace ArcMapConditions.App.ViewModels;

/// <summary>
/// Drives the whole overlay: refreshes the page every RefreshSeconds, parses it,
/// and ticks the visible countdowns every second (independent of the refresh).
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly MapConditionsService _service;
    private readonly SubscriptionManager _subs = new();
    private readonly NotificationService _notify = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _tickTimer;
    private CancellationTokenSource? _inFlight;

    // How often the page is re-downloaded, seconds (matches the skin default).
    private const int RefreshSeconds = 60;

    private string _statusText = "Loading map conditions…";
    private string _activeLabel = "ACTIVE NOW";
    private string _upcomingLabel = "COMING UP";
    private bool _hasActive;
    private bool _isBusy;

    public MainViewModel() : this(new MapConditionsService()) { }

    public MainViewModel(MapConditionsService service)
    {
        _service = service;

        RefreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !_isBusy);
        OpenSiteCommand = new RelayCommand(OpenSite);

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(RefreshSeconds) };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => TickCountdowns();
    }

    public ObservableCollection<ConditionRow> Active { get; } = new();

    public ObservableCollection<ConditionRow> Upcoming { get; } = new();

    public ICommand RefreshCommand { get; }

    public ICommand OpenSiteCommand { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ActiveLabel
    {
        get => _activeLabel;
        private set => SetProperty(ref _activeLabel, value);
    }

    public string UpcomingLabel
    {
        get => _upcomingLabel;
        private set => SetProperty(ref _upcomingLabel, value);
    }

    /// <summary>Hides the whole "Active now" section when nothing is live.</summary>
    public bool HasActive
    {
        get => _hasActive;
        private set => SetProperty(ref _hasActive, value);
    }

    /// <summary>Starts the timers and performs the first fetch.</summary>
    public void Start()
    {
        _tickTimer.Start();
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isBusy)
            return;

        _isBusy = true;
        CommandManager.InvalidateRequerySuggested();

        _inFlight?.Cancel();
        _inFlight = new CancellationTokenSource();
        CancellationToken ct = _inFlight.Token;

        try
        {
            string? html = await _service.FetchHtmlAsync(ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested)
                return;

            ParsedConditions parsed = ConditionsParser.Parse(html, DateTime.Now);
            if (parsed.IsValid)
                Apply(parsed);
            else if (Active.Count == 0 && Upcoming.Count == 0)
                StatusText = "No schedule data yet — check your connection";
            // else: keep the last good data, leave status as-is.
        }
        finally
        {
            _isBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Apply(ParsedConditions parsed)
    {
        DateTime now = DateTime.Now;

        // Keep stored reminder times in sync with the freshly parsed schedule.
        _subs.UpdateTargets(parsed.Upcoming);

        Active.Clear();
        foreach (MapConditionEntry e in parsed.Active)
            Active.Add(new ConditionRow(e, isActive: true, _subs));

        Upcoming.Clear();
        foreach (MapConditionEntry e in parsed.Upcoming)
            Upcoming.Add(new ConditionRow(e, isActive: false, _subs));

        HasActive = Active.Count > 0;
        ActiveLabel = Active.Count > 0 ? $"ACTIVE NOW ({Active.Count})" : "ACTIVE NOW";
        UpcomingLabel = Upcoming.Count > 0 ? $"COMING UP ({Upcoming.Count})" : "COMING UP";

        StatusText = (Active.Count == 0 && Upcoming.Count == 0)
            ? "No schedule data yet — check your connection"
            : $"Updated {now:HH:mm:ss}";
    }

    private void TickCountdowns()
    {
        DateTime now = DateTime.Now;
        foreach (ConditionRow row in Active)
            row.Tick(now);
        foreach (ConditionRow row in Upcoming)
            row.Tick(now);

        // Fire reminders for any subscribed events that have just started.
        foreach (Subscription due in _subs.CollectDue(now))
            _notify.Notify(due);
    }

    private static void OpenSite()
    {
        try
        {
            Process.Start(new ProcessStartInfo(MapConditionsService.PageUrl) { UseShellExecute = true });
        }
        catch
        {
            // Ignore: no default browser / blocked.
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _tickTimer.Stop();
        _inFlight?.Cancel();
        _inFlight?.Dispose();
        _service.Dispose();
        _notify.Dispose();
    }
}
