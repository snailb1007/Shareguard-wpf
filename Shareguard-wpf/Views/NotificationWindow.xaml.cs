using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ShareGuard.App.Views;

/// <summary>
/// Interaction logic for NotificationWindow.xaml
/// </summary>
public partial class NotificationWindow : Window
{
    private readonly DispatcherTimer _closeTimer;
    private readonly double _targetY;

    public NotificationWindow(string title, string message, double targetY)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        _targetY = targetY;

        // Setup timer to start fade out after 3 seconds
        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _closeTimer.Tick += OnCloseTimerTick;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        double margin = 10;
        double endX = SystemParameters.WorkArea.Right - Width - margin;

        // Start position: slide in from the right edge
        Left = SystemParameters.WorkArea.Right;
        Top = _targetY;
        Opacity = 0;

        // Slide-in animation on Left property
        var slideAnim = new DoubleAnimation
        {
            From = SystemParameters.WorkArea.Right,
            To = endX,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        // Fade-in animation on Opacity
        var fadeAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(LeftProperty, slideAnim);
        BeginAnimation(OpacityProperty, fadeAnim);

        _closeTimer.Start();
    }

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        FadeOutAndClose();
    }

    private void FadeOutAndClose()
    {
        var fadeOutAnim = new DoubleAnimation
        {
            From = Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOutAnim.Completed += (s, ev) => Close();

        BeginAnimation(OpacityProperty, fadeOutAnim);
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Close immediately on click
        _closeTimer.Stop();
        Close();
    }
}
