using System;
using System.Globalization;
using LiteDB;
using StainsGate;

namespace AvaloniaClient.Models;

public class ChatModel
{
    [BsonId] public string ChatId { get; set; }
    public string OwnerUsername { get; set; }

    public string MateName { get; set; }
    public DateTime CreatedAt { get; set; }
    public EncryptAlgo Algorithm { get; set; }
    public EncryptMode CipherMode { get; set; }
    public PaddingMode Padding { get; set; }

    public RoomData GetRoomData()
    {
        return new RoomData()
        {
            Padding = Padding,
            CipherMode = CipherMode,
            Algo = Algorithm
        };
    }

    public ChatModel() { }

    public ChatModel(RoomInfo roomInfo)
    {
        ChatId = roomInfo.ChatId;
        OwnerUsername = roomInfo.OwnerUsername;
        CipherMode = roomInfo.Settings.CipherMode;
        Padding = roomInfo.Settings.Padding;
        Algorithm = roomInfo.Settings.Algo;
        MateName = roomInfo.OtherSubscriber;
        CreatedAt = DateTime.TryParse(roomInfo.CreationTime,
            new CultureInfo("ru-RU"),
            DateTimeStyles.RoundtripKind,
            out var dt)
            ? 
            dt.ToLocalTime() : DateTime.UtcNow;
    }
    
}