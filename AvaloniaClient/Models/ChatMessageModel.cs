using System;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaClient.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using LiteDB;
using StainsGate;

namespace AvaloniaClient.Models;

public partial class ChatMessageModel : ViewModelBase
{
    [BsonField("chatId")] public string ChatId { get; set; }
    
    [BsonField("sender")] [ObservableProperty] private string _sender;
    [BsonField("content")][ObservableProperty] private string _content; // for message it's text, for other's it's path
    [BsonField("timestamp")][ObservableProperty] private DateTime _timestamp;
    [BsonField("isSentByMe")][ObservableProperty] private bool _isSentByMe;
    [BsonField("messageType")][ObservableProperty] private MessageType _messageType;
    [BsonField("filename")] [ObservableProperty] private string? _filename = null;

    public ChatMessageModel(string chatId, string sender, string content, DateTime timestamp, bool isSentByMe, MessageType messageType, string? filename)
    {
        ChatId = chatId;
        _sender = sender;
        _content = content;
        _timestamp = timestamp;
        _isSentByMe = isSentByMe;
        _messageType = messageType;
        _filename = filename;
    }
    
    public Bitmap? ImageBitmap
    {
        get
        {
            if (MessageType == MessageType.Image && File.Exists(Content!))
            {
                return new Bitmap(Content!);
            }
            else
            {
                var uri = new Uri($"avares://AvaloniaClient/Assets/no_file_icon.png");
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
        }
    }
}