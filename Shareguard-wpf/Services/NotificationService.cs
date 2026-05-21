using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Animation;
using ShareGuard.App.Views;
using ShareGuard.Application.Interfaces;

namespace ShareGuard.App.Services;

/// <summary>
/// Service that creates, positions, and manages the lifecycle of slide-in toast notifications.
/// Supports stacking multiple notifications vertically.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly List<NotificationWindow> _activeNotifications = new();
    private readonly ISettingsService _settingsService;

    public NotificationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void ShowNotification(string title, string message)
    {
        var settings = _settingsService.Load();
        if (!settings.ShowCleanNotifications)
        {
            return;
        }

        if (System.Windows.Application.Current == null)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            double margin = 10;
            double spacing = 5;
            double windowHeight = NotificationWindow.DefaultHeight;
            double targetY = SystemParameters.WorkArea.Bottom - windowHeight - margin - (_activeNotifications.Count * (windowHeight + spacing));

            var notification = new NotificationWindow(title, message, targetY);
            
            notification.Closed += (s, e) =>
            {
                _activeNotifications.Remove(notification);
                RearrangeNotifications();
            };

            _activeNotifications.Add(notification);
            notification.Show();
        });
    }

    private void RearrangeNotifications()
    {
        double margin = 10;
        double spacing = 5;
        
        for (int i = 0; i < _activeNotifications.Count; i++)
        {
            var window = _activeNotifications[i];
            double endY = SystemParameters.WorkArea.Bottom - window.Height - margin - (i * (window.Height + spacing));
            
            // Smoothly animate the remaining notification windows to their new Y positions
            var moveAnim = new DoubleAnimation
            {
                To = endY,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            window.BeginAnimation(Window.TopProperty, moveAnim);
        }
    }
}
