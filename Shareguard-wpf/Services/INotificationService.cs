namespace ShareGuard.App.Services;

/// <summary>
/// Service to display system notifications/toasts.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Displays a temporary slide-in toast notification.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The description or body text of the notification.</param>
    void ShowNotification(string title, string message);
}
