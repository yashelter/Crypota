using StainsGate; 
using Grpc.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Serilog; 
using Avalonia.Threading;

namespace AvaloniaClient.Models;


public class ChatSessionContext : IDisposable
{
    public string ChatId { get; }
    

    public EncryptAlgo Algorithm { get; set; }
    public EncryptMode Mode { get; set; }
    public PaddingMode ChatPadding { get; set; }

    public CancellationTokenSource? MessageReceivingCts { get; private set; }
    public bool IsSubscribedToMessages => MessageReceivingCts != null && !MessageReceivingCts.IsCancellationRequested;
    
    public event Action<ChatMessageModel>? OnMessageReceived;
    public event Action<string, string>? OnMessageSubscriptionError;
    
    public event Action<string>? OnSubscriptionStarted;
    public event Action<string>? OnSubscriptionStopped;

    private readonly HackingGate.HackingGateClient _grpcClient;
    private readonly ChatSessionStarter _auth;
    
    private EncryptingManager _encryptingManager; // apply it
    
    public ChatSessionContext(string chatId, HackingGate.HackingGateClient grpcClient, ChatSessionStarter auth)
    {
        ChatId = chatId;
        _grpcClient = grpcClient ?? throw new ArgumentNullException(nameof(grpcClient));
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        
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
       
       
       _ = Task.Run(async () => await ReceiveMessagesLoopAsync(MessageReceivingCts!.Token), MessageReceivingCts!.Token)
           .ContinueWith(t =>
           {
               if (t.IsFaulted)
               {
                   Log.Error(t.Exception, "Ошибка в цикле получения сообщений для чата {0}", ChatId);
               }
               OnSubscriptionStopped?.Invoke(ChatId);
           }, TaskScheduler.Default); 
       
       OnSubscriptionStarted?.Invoke(ChatId);
    }
    

    private async Task ReceiveMessagesLoopAsync(CancellationToken ct)
    {
        Log.Information("Запущен цикл получения сообщений для чата {ChatId}", ChatId);
        try
        {
            var request = new RoomPassKey { ChatId = this.ChatId };
            using var call = _grpcClient.ReceiveMessages(request, cancellationToken: ct);

            await foreach (var message in call.ResponseStream.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested)
                {
                    Log.Information("Получение сообщений для чата {ChatId} отменено.", ChatId);
                    break;
                }

                Log.Debug("Получено сообщение Protobuf для чата {ChatId}, тип: {MessageType}", ChatId, message.Type);

                byte[] decryptedData;
                try
                {
                    decryptedData =  _encryptingManager.DecryptMessage(message.Data.ToByteArray());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка расшифровки сообщения в чате {0}", ChatId);
                    await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(new ChatMessageModel("Система", "[Ошибка расшифровки сообщения]", DateTime.UtcNow, false, MessageType.Message)));
                    continue;
                }

                string messageContent = System.Text.Encoding.UTF8.GetString(decryptedData);
                DateTime timestamp = DateTime.TryParse(message.Timestamp, out var dt) ? dt.ToLocalTime() : DateTime.UtcNow;

                var chatMessage = new ChatMessageModel(
                    message.FromUsername,
                    messageContent,
                    timestamp,
                    false,
                    message.Type
                );

                // Уведомляем подписчиков (ViewModel) о новом сообщении в UI потоке
                await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(chatMessage));
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            Log.Information("Поток получения сообщений для чата {ChatId} был отменен.", ChatId);
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "gRPC ошибка при получении сообщений для чата {ChatId}", ChatId);
            OnMessageSubscriptionError?.Invoke(ChatId, $"Ошибка gRPC: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Общая ошибка в цикле получения сообщений для чата {ChatId}", ChatId);
            OnMessageSubscriptionError?.Invoke(ChatId, $"Внутренняя ошибка: {ex.Message}");
        }
        finally
        {
            Log.Information("Завершен цикл получения сообщений для чата {ChatId}", ChatId);
        }
    }

    

    public void CancelMessageSubscription()
    {
        if (MessageReceivingCts != null && !MessageReceivingCts.IsCancellationRequested)
        {
            Log.Debug("Отмена подписки на сообщения для чата {ChatId}", ChatId);
            MessageReceivingCts.Cancel();
            MessageReceivingCts?.Dispose();
        }
    }
    
    
    private bool _disposed = false;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            Log.Debug("Dispose ChatSessionContext для ChatId: {ChatId}", ChatId);
            CancelMessageSubscription();
            MessageReceivingCts?.Dispose();
            MessageReceivingCts = null;
        }
        _disposed = true;
    }
    ~ChatSessionContext() { Dispose(false); }
}