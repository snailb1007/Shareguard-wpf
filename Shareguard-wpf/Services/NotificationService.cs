using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Animation;
using ShareGuard.App.Views;

namespace ShareGuard.App.Services;

/// <summary>
/// Service that creates, positions, and manages the lifecycle of slide-in toast notifications.
/// Supports stacking multiple notifications vertically.
/// </summary>
public class NotificationService : INotificationService
{
    private static readonly List<NotificationWindow> _activeNotifications = new();
    private static readonly object _lock = new();

    public void ShowNotification(string title, string message)
    {
        if (System.Windows.Application.Current == null)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            lock (_lock)
            {
                double margin = 10;
                double spacing = 5;
                double windowHeight = 80;
                double targetY = SystemParameters.WorkArea.Bottom - windowHeight - margin - (_activeNotifications.Count * (windowHeight + spacing));

                var notification = new NotificationWindow(title, message, targetY);
                
                notification.Closed += (s, e) =>
                {
                    lock (_lock)
                    {
                        _activeNotifications.Remove(notification);
                        RearrangeNotifications();
                    }
                };

                _activeNotifications.Add(notification);
                notification.Show();
            }
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
