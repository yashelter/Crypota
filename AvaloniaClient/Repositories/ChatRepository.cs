using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaClient.Contexts;
using AvaloniaClient.Models;

namespace AvaloniaClient.Repositories;


public sealed class ChatRepository : IDisposable
{
    private readonly LiteDbContext _ctx;

    public ChatRepository()
    {
        _ctx = new LiteDbContext();
        _ctx.Messages.EnsureIndex(x => x.ChatId);
    }


    public IEnumerable<ChatModel> GetAllChats() => _ctx.Chats.FindAll();

    public ChatModel? GetChat(string chatId) => _ctx.Chats.FindById(chatId);

    public void AddChat(ChatModel chat) => _ctx.Chats.Insert(chat);

    public void DeleteChat(string chatId)
    {
        _ctx.Chats.Delete(chatId);
        _ctx.Messages.DeleteMany(m => m.ChatId == chatId);
    }


    public IEnumerable<ChatMessageModel> GetMessages(string chatId)
        => _ctx.Messages
            .Find(m => m.ChatId == chatId)
            .OrderBy(m => m.Timestamp);

    public void AddMessage(ChatMessageModel message) => _ctx.Messages.Insert(message);

    public void DeleteMessage(Guid messageId) => _ctx.Messages.Delete(messageId);

    public void UpdateMessage(ChatMessageModel message) => _ctx.Messages.Update(message);

    public void Dispose() => _ctx.Dispose();
}
