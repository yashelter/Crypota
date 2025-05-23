using System;
using System.IO;
using AvaloniaClient.Models;
using AvaloniaClient.Services;
using LiteDB;

namespace AvaloniaClient.Contexts;

public sealed class LiteDbContext : IDisposable
{
    private readonly LiteDatabase _db;

    public LiteDbContext()
    {
        _db = new LiteDatabase(Config.Instance.AppDataBase);
    }

    public ILiteCollection<ChatModel> Chats => _db.GetCollection<ChatModel>("chats");
    public ILiteCollection<ChatMessageModel> Messages => _db.GetCollection<ChatMessageModel>("messages");

    public static void ClearDb()
    {
        File.Delete(Config.Instance.AppDataBase);
    }

    public void Dispose() => _db?.Dispose();
}