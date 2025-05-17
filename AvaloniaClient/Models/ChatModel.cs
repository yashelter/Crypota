using System;
using LiteDB;
using StainsGate;

namespace AvaloniaClient.Models;

public class ChatModel
{
    [BsonId] public string ChatId { get; set; }
    public string OwnerUsername { get; set; } 
    
    public DateTime CreatedAt { get; set; }
    public EncryptAlgo Algorithm  { get; set; } 
    public EncryptMode CipherMode { get; set; } 
    public PaddingMode Padding { get; set; } 
}