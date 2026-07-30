using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ArcMapConditions.App.Services;
using ArcMapConditions.App.ViewModels;

namespace ArcMapConditions.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AppSettings _settings = AppSettings.Load();
    private bool _positioned;
    private bool _adjusting;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += OnLoaded;
        SizeChanged += (_, _) => AdjustBodyHeight();
        LocationChanged += (_, _) => { RecordPosition(); AdjustBodyHeight(); };
        Closing += OnClosing;
        Closed += (_, _) => _vm.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RestoreOrDefaultPosition();
        AdjustBodyHeight();
        StartupMenuItem.IsChecked = StartupManager.IsEnabled();
        RememberPosMenuItem.IsChecked = _settings.RememberPosition;
        _vm.Start();
    }

    // ----- Position: save / restore (multi-monitor aware) -------------------

    /// <summary>
    /// Restores the last saved position when enabled and still visible on a
    /// currently connected monitor; otherwise parks in the top-right corner of
    /// the primary work area.
    /// </summary>
    private void RestoreOrDefaultPosition()
    {
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : 200;

        if (_settings.RememberPosition &&
            _settings.WindowLeft is double left &&
            _settings.WindowTop is double top &&
            IsOnScreen(left, top, w, h))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - w - 8;
            Top = area.Top + 8;
        }

        _positioned = true;
    }

    private void RecordPosition()
    {
        if (!_positioned || _adjusting)
            return;
        if (double.IsNaN(Left) || double.IsNaN(Top))
            return;

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
    }

    /// <summary>
    /// True if enough of the window (a draggable strip) lands inside the virtual
    /// desktop that spans all connected monitors — so a position saved on a
    /// monitor that has since been unplugged is rejected.
    /// </summary>
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        var win = new Rect(left, top, width, height);
        win.Intersect(new Rect(vLeft, vTop, vWidth, vHeight));
        if (win.IsEmpty)
            return false;

        // The header must remain reachable to drag the widget.
        return win.Width >= 120 && win.Height >= 30
            && top >= vTop - 4 && top <= vTop + vHeight - 30;
    }

    // ----- Auto height (kept within the desktop, never yanked between screens) --

    /// <summary>
    /// Lets the window grow to fit its content but caps the scroll body so it
    /// fits vertically, and only pulls it back if it leaves the whole virtual
    /// desktop — never snapping it from a secondary monitor to the primary one.
    /// </summary>
    private void AdjustBodyHeight()
    {
        if (_adjusting || !_positioned)
            return;

        _adjusting = true;
        try
        {
            double reserved = ActualHeight - BodyScroller.ActualHeight;
            if (double.IsNaN(reserved) || reserved < 0)
                reserved = 150;

            const double bottomGap = 10;

            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth;
            double vBottom = vTop + SystemParameters.VirtualScreenHeight;

            // Cap by the space down to the desktop bottom, but never taller than
            // a typical monitor (primary work area) so it stays sane on mixed
            // monitor sizes.
            double byDesktop = vBottom - Top - reserved - bottomGap;
            double byMonitor = SystemParameters.WorkArea.Height - reserved - bottomGap;
            double maxHeight = Math.Max(160, Math.Min(byDesktop, byMonitor));

            if (Math.Abs(BodyScroller.MaxHeight - maxHeight) > 0.5)
                BodyScroller.MaxHeight = maxHeight;

            // Only correct if it left the entire virtual desktop.
            if (Left + ActualWidth > vRight)
                Left = vRight - ActualWidth - 8;
            if (Left < vLeft)
                Left = vLeft + 8;
        }
        finally
        {
            _adjusting = false;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_settings.RememberPosition && !double.IsNaN(Left) && !double.IsNaN(Top))
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }
        _settings.Save();
    }

    // ----- Window chrome / menu ---------------------------------------------

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e) => TogglePin();

    private void PinMenuItem_Click(object sender, RoutedEventArgs e) => TogglePin();

    private void TogglePin()
    {
        Topmost = !Topmost;
        PinMenuItem.IsChecked = Topmost;
        PinButton.Opacity = Topmost ? 1.0 : 0.45;
    }

    private void StartupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // The checkable item has already toggled by the time Click fires;
        // apply it, then reflect the real registry state (in case it failed).
        StartupMenuItem.IsChecked = StartupManager.SetEnabled(StartupMenuItem.IsChecked);
    }

    private void RememberPosMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _settings.RememberPosition = RememberPosMenuItem.IsChecked;
        if (_settings.RememberPosition)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
        }
        _settings.Save();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();
}
