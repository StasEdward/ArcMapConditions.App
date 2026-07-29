using System;
using System.Windows;
using System.Windows.Threading;
using ArcMapConditions.App.Services;

namespace ArcMapConditions.App;

/// <summary>
/// A small bottom-right reminder popup shown when a subscribed event starts.
/// Does not steal focus (ShowActivated=False), auto-dismisses, and stacks when
/// several fire at once.
/// </summary>
public partial class ToastWindow : Window
{
    private static int _openSlots;

    private static readonly TimeSpan AutoCloseAfter = TimeSpan.FromSeconds(15);

    private readonly int _slot;
    private DispatcherTimer? _autoClose;

    public ToastWindow(string condition, string map, string iconSlug)
    {
        InitializeComponent();

        _slot = _openSlots++;

        IconImage.Source = IconProvider.Get(iconSlug);
        TitleText.Text = string.IsNullOrEmpty(map) ? condition : $"{condition}  •  {map}";
        SubText.Text = $"started at {DateTime.Now:HH:mm}";

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rect area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 8;
        Top = area.Bottom - ActualHeight - 8 - _slot * (ActualHeight + 6);

        _autoClose = new DispatcherTimer { Interval = AutoCloseAfter };
        _autoClose.Tick += (_, _) => Close();
        _autoClose.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _autoClose?.Stop();
        if (_openSlots > 0)
            _openSlots--;
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();
}
