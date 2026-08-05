using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ArcMapConditions.App.Services;
using ArcMapConditions.App.ViewModels;
using System.Windows.Forms; // Added for Screen.FromPoint

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
            IsOnScreen(left, top, w, h) &&
            IsOnSameScreen(left, top))
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

        // Ensure window is fully visible on screen
        EnsureWindowIsVisible(w, h);
        
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
        // Save current screen ID
        try
        {
            _settings.LastScreenId = GetScreenId();
        }
        catch
        {
            // If we can't determine the screen, just continue without saving it
            _settings.LastScreenId = null;
        }
    }

    /// <summary>
    /// True if enough of the window (a draggable strip) lands inside the virtual
    /// desktop that spans all connected monitors — so a position saved on a
    /// monitor that has since been unplugged is rejected.
    /// </summary>
    private bool IsOnScreen(double left, double top, double width, double height)
    {
        // Handle rotated screens and negative coordinates properly
        double vLeft = SystemParameters.VirtualScreenLeft;
        double vTop = SystemParameters.VirtualScreenTop;
        double vWidth = SystemParameters.VirtualScreenWidth;
        double vHeight = SystemParameters.VirtualScreenHeight;

        // Create proper virtual screen boundaries (handle negative coordinates)
        double actualLeft = Math.Min(vLeft, vLeft + vWidth);
        double actualTop = Math.Min(vTop, vTop + vHeight);
        double actualRight = Math.Max(vLeft, vLeft + vWidth);
        double actualBottom = Math.Max(vTop, vTop + vHeight);

        // Check if window position is within virtual screen boundaries
        var win = new Rect(left, top, width, height);
        var virtualScreen = new Rect(actualLeft, actualTop, actualRight - actualLeft, actualBottom - actualTop);
        
        // Check intersection with virtual screen
        win.Intersect(virtualScreen);
        if (win.IsEmpty)
            return false;

        // The header must remain reachable to drag the widget.
        double effectiveTop = Math.Min(top, top + height);
        double effectiveBottom = Math.Max(top, top + height);
        
        return win.Width >= 120 && win.Height >= 30
            && effectiveTop >= virtualScreen.Top - 4 
            && effectiveBottom <= virtualScreen.Bottom - 30;
    }

    /// <summary>
    /// Checks if the window position is on the same screen as before
    /// </summary>
    private bool IsOnSameScreen(double left, double top)
    {
        if (string.IsNullOrEmpty(_settings.LastScreenId))
            return true; // No previous screen info, assume it's okay

        try
        {
            // For rotated screens, we need to be more careful about position checking
            string currentScreenId = GetScreenId();
            
            // If the saved screen ID matches current screen ID, we can restore position
            if (string.Equals(_settings.LastScreenId, currentScreenId, StringComparison.OrdinalIgnoreCase))
                return true;
                
            // If screens don't match, check if window is still visible on any screen
            // This handles cases where user moved window to different monitor but it's still valid
            double vLeft = SystemParameters.VirtualScreenLeft;
            double vTop = SystemParameters.VirtualScreenTop;
            double vWidth = SystemParameters.VirtualScreenWidth;
            double vHeight = SystemParameters.VirtualScreenHeight;

            // Create proper boundaries for virtual screen
            double actualLeft = Math.Min(vLeft, vLeft + vWidth);
            double actualTop = Math.Min(vTop, vTop + vHeight);
            double actualRight = Math.Max(vLeft, vLeft + vWidth);
            double actualBottom = Math.Max(vTop, vTop + vHeight);

            // Check if the saved position is within any currently connected screen
            return left >= actualLeft - 100 && 
                   top >= actualTop - 100 && 
                   left <= actualRight + 100 && 
                   top <= actualBottom + 100;
        }
        catch
        {
            // If we can't determine the screen, assume it's okay to restore position
            return true;
        }
    }

    /// <summary>
    /// Gets a unique identifier for the current screen
    /// </summary>
    private string GetScreenId()
    {
        try
        {
            // Get the screen where this window is currently located
            var screen = Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
            return screen.DeviceName;
        }
        catch
        {
            // If we can't determine the screen, return a default identifier
            return "default";
        }
    }

    /// <summary>
    /// Ensures the window is fully visible on screen by adjusting position if needed
    /// </summary>
    private void EnsureWindowIsVisible(double width, double height)
    {
        Rect virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        // Handle negative coordinates properly for rotated screens
        double adjustedLeft = Math.Max(Left, virtualScreen.Left);
        double adjustedTop = Math.Max(Top, virtualScreen.Top);
        
        if (adjustedLeft != Left)
            Left = adjustedLeft;
        if (adjustedTop != Top)
            Top = adjustedTop;
            
        if (Left + width > virtualScreen.Right)
            Left = virtualScreen.Right - width - 8;
        if (Top + height > virtualScreen.Bottom)
            Top = virtualScreen.Bottom - height - 8;
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
                
            // Ensure window is fully visible vertically
            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : 200;
            EnsureWindowIsVisible(w, h);
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
            try
            {
                _settings.LastScreenId = GetScreenId();
            }
            catch
            {
                // If we can't determine the screen, just continue without saving it
                _settings.LastScreenId = null;
            }
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
            try
            {
                _settings.LastScreenId = GetScreenId();
            }
            catch
            {
                // If we can't determine the screen, just continue without saving it
                _settings.LastScreenId = null;
            }
        }
        _settings.Save();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();
}
