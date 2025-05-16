using System;
using Avalonia.Notification;
using Serilog;

namespace AvaloniaClient.Models;

/// <summary>
/// Encapsulates toasts and makes logging
/// </summary>
public class ToastManager
{
    private readonly INotificationMessageManager _manager;
    
    public ToastManager(INotificationMessageManager manager)
    {
        _manager = manager;
    }

    public void ShowErrorMessageToast(string message)
    {
        Log.Verbose(message);
        _manager.CreateMessage()
            .Accent("#D32F2F")
            .Animates(true)
            .Background("#333")
            .HasBadge(":(")
            .HasMessage(message)
            .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
            .Queue();
    }

    public void ShowSuccessMessageToast(string message)
    {
        Log.Verbose(message);
        _manager.CreateMessage()
            .Accent("#1751C3")
            .Animates(true)
            .Background("#333")
            .HasBadge(":)")
            .HasMessage(message)
            .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
            .Queue();
    }
}