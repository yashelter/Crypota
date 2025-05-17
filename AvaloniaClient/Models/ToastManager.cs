using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
    
    public void  TODO() // will be progress bar
    {
        _manager
            .CreateMessage()
            .Accent("#F15B19")
            .Background("#F15B19")
            .HasHeader("Lost connection to server")
            .HasMessage("Reconnecting...")
            .WithOverlay(new ProgressBar
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 3,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                Background = Brushes.Transparent,
                IsIndeterminate = true,
                IsHitTestVisible = false
            })
            .Queue();
    }

    
    public INotificationMessage ShowCancelPanel(string message, Action cancelAction)
    {
        Log.Verbose(message);
        return _manager.CreateMessage()
            .Accent("#535BD3")
            .Animates(true)
            .Background("#D32EB2")
            .HasBadge("...")
            .HasMessage(message)
            .Dismiss().WithButton("Отмена", _ => { cancelAction.Invoke(); })
            .Queue();
    }

    public void DismissMessage(INotificationMessage notification)
    {
        Log.Verbose(notification.ToString() ?? string.Empty);
        _manager.Dismiss(notification);
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