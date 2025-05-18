using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaClient.Models;
using Google.Protobuf;
using Grpc.Core;
using Serilog;
using StainsGate;

namespace AvaloniaClient.Contexts;


public sealed class ChatSessionContext : IDisposable
{
    public string ChatId { get; }

    public EncryptAlgo Algorithm { get; set; }
    public EncryptMode Mode { get; set; }
    public PaddingMode ChatPadding { get; set; }

    public CancellationTokenSource? MessageReceivingCts { get; private set; }
    public bool IsSubscribedToMessages => MessageReceivingCts != null && !MessageReceivingCts.IsCancellationRequested;
    
    public event Action<string, ChatMessageModel>? OnMessageReceived;
    public event Action<string, string>? OnMessageError;
    
    public event Action<string>? OnSubscriptionStarted;
    public event Action<string>? OnSubscriptionStopped;

    private readonly HackingGate.HackingGateClient _grpcClient;
    private readonly ChatSessionStarter _auth;
    public ChatSessionStarter SessionStarter => _auth;

    private readonly EncryptingManager _encryptingManager; // apply it

    
    public ChatSessionContext(string chatId, HackingGate.HackingGateClient grpcClient, RoomData roomData) :
        this(grpcClient, chatId, new EncryptingManager(roomData, null),
            new ChatSessionStarter(chatId, grpcClient))
    {
        
    }
    
    public ChatSessionContext(HackingGate.HackingGateClient grpcClient, RoomInfo roomInfo) :
        this(grpcClient, roomInfo.ChatId, new EncryptingManager(roomInfo.Settings, null),
            new ChatSessionStarter(roomInfo.ChatId, grpcClient))
    {
        
    }


    public ChatSessionContext(HackingGate.HackingGateClient grpcClient, string chatId, EncryptingManager encryptingManager, ChatSessionStarter auth)
    {
        ChatId = chatId;
        _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _encryptingManager = encryptingManager ?? throw new ArgumentNullException(nameof(encryptingManager));
        
        Log.Debug("ChatSessionContext создан для ChatId: {ChatId}", ChatId);
    }

    
    public async Task InitializeSessionAsync(CancellationToken externalCt = default)
    {
        if (MessageReceivingCts is not null)
        {
            Log.Warning("Попытка открыть уже открытую сессию");
            return;
        }
        
        MessageReceivingCts = new CancellationTokenSource();
            
       _auth.ResetDhState();
       await _auth.InitializeSessionAsync(externalCt);
       _encryptingManager.SetKey(_auth.SharedSecret!);

       if (externalCt.IsCancellationRequested)
       {
           CancelMessageSubscription();
           return;
       }
       
       _ = Task.Run(async () => await ReceiveMessagesLoopAsync(MessageReceivingCts!.Token), MessageReceivingCts!.Token)
           .ContinueWith(t =>
           {
               if (t.IsFaulted)
               {
                   Log.Error(t.Exception, "Ошибка в цикле получения сообщений для чата {0}", ChatId);
               }
               Dispatcher.UIThread.InvokeAsync(() => OnSubscriptionStopped?.Invoke(ChatId));
           }, TaskScheduler.Default); 
       
       OnSubscriptionStarted?.Invoke(ChatId);
    }
    

    private async Task ReceiveMessagesLoopAsync(CancellationToken ct)
    {
        Log.Information("Запущен цикл получения сообщений для чата {0}", ChatId);
        try
        {
            var request = new RoomPassKey { ChatId = this.ChatId };
            using var call = _grpcClient.ReceiveMessages(request, cancellationToken: ct);

            await foreach (var message in call.ResponseStream.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested)
                {
                    Log.Information("Получение сообщений для чата {0} отменено.", ChatId);
                    break;
                }

                Log.Debug("Получено сообщение Protobuf для чата {0}, тип: {1}", ChatId, message.Type);

                if (message.Type == MessageType.NewIv)
                {
                    _encryptingManager.SetIv(message.Data.ToByteArray());
                }

                byte[] decryptedData;
                try
                {
                    decryptedData =  _encryptingManager.DecryptMessage(message.Data.ToByteArray());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка расшифровки сообщения в чате {0}", ChatId);
                    await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(ChatId, new ChatMessageModel( ChatId, "Система", "[Ошибка расшифровки сообщения]", DateTime.UtcNow, false, MessageType.Message)));
                    continue;
                }

                string messageContent = System.Text.Encoding.UTF8.GetString(decryptedData);
                DateTime timestamp = DateTime.TryParse(message.Timestamp, new CultureInfo("ru-RU"), DateTimeStyles.RoundtripKind, out var dt) ? 
                    dt.ToLocalTime() : DateTime.UtcNow;

                var chatMessage = new ChatMessageModel(
                    ChatId,
                    message.FromUsername,
                    messageContent,
                    timestamp,
                    false,
                    message.Type
                );

                // Уведомляем подписчиков (ViewModel) о новом сообщении в UI потоке
                await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(ChatId, chatMessage));
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Log.Information("Поток получения сообщений для чата {ChatId} был отменен.", ChatId);
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "gRPC ошибка при получении сообщений для чата {ChatId}", ChatId);
            await Dispatcher.UIThread.InvokeAsync(() =>  OnMessageError?.Invoke(ChatId, $"Ошибка gRPC: {ex.Status.Detail}"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Общая ошибка в цикле получения сообщений для чата {ChatId}", ChatId);
            await Dispatcher.UIThread.InvokeAsync(() =>  OnMessageError?.Invoke(ChatId, $"Внутренняя ошибка: {ex.Message}"));
        }
        finally
        {
            Log.Information("Завершен цикл получения сообщений для чата {ChatId}", ChatId);
            await Dispatcher.UIThread.InvokeAsync(() =>  OnSubscriptionStopped?.Invoke(ChatId));
        }
    }
    
    public void CancelMessageSubscription()
    {
        if (MessageReceivingCts != null && !MessageReceivingCts.IsCancellationRequested)
        {
            Log.Debug("Отмена подписки на сообщения для чата {0}", ChatId);
            MessageReceivingCts.Cancel();
            MessageReceivingCts?.Dispose();
            MessageReceivingCts = null;
        }
    }


    public async Task SendMessage(ChatMessageModel message)
    {
        if (!IsSubscribedToMessages)
        {
            Log.Warning("Нельзя отправить сообщение без обмена ключом");
            OnMessageError?.Invoke(ChatId, $"Нельзя отправить сообщение без обмена ключом");
            return;
        }

        try
        {
            var content = await Task.Run(() => Encoding.UTF8.GetBytes(message.Content));

            await _grpcClient.SendMessageAsync(new Message()
            {
                ChatId = this.ChatId,
                Data = ByteString.CopyFrom(_encryptingManager.EncryptMessage(content)),
                Timestamp = message.Timestamp.ToString("o"),
                Type = message.MessageType,
                FromUsername = message.Sender
            });
            Log.Debug("Sended message: {0}, to chat {1}", content, ChatId);
            OnMessageReceived?.Invoke(ChatId, new ChatMessageModel( ChatId, message.Sender, message.Content, message.Timestamp, true, MessageType.Message));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось отправить сообщение {0}", ChatId);
            OnMessageError?.Invoke(ChatId, $"Внутренняя ошибка: {ex.Message}");
        }
    }
    
    
    private bool _disposed = false;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            Log.Debug("Dispose ChatSessionContext для ChatId: {0}", ChatId);
            CancelMessageSubscription();
            MessageReceivingCts?.Dispose();
            MessageReceivingCts = null;
        }
        _disposed = true;
    }
    ~ChatSessionContext() { Dispose(false); }
}