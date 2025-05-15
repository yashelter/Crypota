using System;
using AvaloniaClient.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaClient.Models;

public partial class ChatMessageModel : ViewModelBase
{
    public Guid Id { get; }
    [ObservableProperty] private string _sender;
    [ObservableProperty] private string _content;
    [ObservableProperty] private DateTime _timestamp;
    [ObservableProperty] private bool _isSentByMe; // Чтобы выравнивать свои сообщения иначе

    public ChatMessageModel(string sender, string content, DateTime timestamp, bool isSentByMe)
    {
        Id = Guid.NewGuid();
        _sender = sender;
        _content = content;
        _timestamp = timestamp;
        _isSentByMe = isSentByMe;
    }
}