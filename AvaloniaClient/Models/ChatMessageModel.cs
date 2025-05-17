using System;
using AvaloniaClient.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;
using StainsGate;

namespace AvaloniaClient.Models;

public partial class ChatMessageModel : ViewModelBase
{
    [BsonField("chatId")] public string ChatId { get; set; }
    
    [BsonField("sender")] [ObservableProperty] private string _sender;
    [BsonField("content")][ObservableProperty] private string _content;
    [BsonField("timestamp")][ObservableProperty] private DateTime _timestamp;
    [BsonField("isSentByMe")][ObservableProperty] private bool _isSentByMe;
    [BsonField("messageType")][ObservableProperty] private MessageType _messageType;

    public ChatMessageModel(string chatId, string sender, string content, DateTime timestamp, bool isSentByMe, MessageType messageType)
    {
        ChatId = chatId;
        _sender = sender;
        _content = content;
        _timestamp = timestamp;
        _isSentByMe = isSentByMe;
        _messageType = messageType;
    }
}