using System;
using System.Windows;
using System.Windows.Input;
using ArcMapConditions.App.ViewModels;

namespace ArcMapConditions.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _positioned;
    private bool _adjusting;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Loaded += OnLoaded;
        SizeChanged += (_, _) => AdjustBodyHeight();
        LocationChanged += (_, _) => AdjustBodyHeight();
        Closed += (_, _) => _vm.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionTopRight();
        AdjustBodyHeight();
        _vm.Start();
    }

    /// <summary>Parks the widget in the top-right corner of the work area (once).</summary>
    private void PositionTopRight()
    {
        Rect area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 8;
        Top = area.Top + 8;
        _positioned = true;
    }

    /// <summary>
    /// Lets the window grow to fit its content, but never past the bottom of the
    /// screen: the scrollable body is capped to the space between the window's
    /// current top and the work-area bottom. So the scrollbar only appears when
    /// there are genuinely more events than fit on screen.
    /// </summary>
    private void AdjustBodyHeight()
    {
        if (_adjusting || !_positioned)
            return;

        _adjusting = true;
        try
        {
            Rect area = SystemParameters.WorkArea;

            // Chrome around the scroll body (header, margins, borders) is
            // constant regardless of how tall the body is.
            double reserved = ActualHeight - BodyScroller.ActualHeight;
            if (double.IsNaN(reserved) || reserved < 0)
                reserved = 150;

            const double bottomGap = 10;
            double available = area.Bottom - Top - reserved - bottomGap;
            double maxHeight = Math.Max(160, available);

            if (Math.Abs(BodyScroller.MaxHeight - maxHeight) > 0.5)
                BodyScroller.MaxHeight = maxHeight;

            // Keep it fully on screen if it grew past the right/bottom edge.
            if (Left + ActualWidth > area.Right)
                Left = area.Right - ActualWidth - 8;
        }
        finally
        {
            _adjusting = false;
        }
    }

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

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();
}
