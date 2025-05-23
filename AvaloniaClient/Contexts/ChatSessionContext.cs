using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaClient.Models;
using AvaloniaClient.Services;
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

    public CancellationTokenSource? MessageReceivingCts { get; private set; } = null;
    public bool IsSubscribedToMessages => MessageReceivingCts != null && !MessageReceivingCts.IsCancellationRequested;
    
    public event Action<string, ChatMessageModel>? OnMessageReceived;
    
    public event Action<Message, ChatMessageModel>? OnFileReceived;
    public event Action<string, string>? OnMessageError;
    
    public event Action<string>? OnSubscriptionStarted;
    public event Action<string>? OnSubscriptionStopped;
    public event Action<string>? OnIvChanged;
    public event Action<string>? OnFileUploadError;


    private readonly ChatSessionStarter _auth;
    public ChatSessionStarter SessionStarter => _auth;

    private readonly EncryptingManager _encryptingManager;

    
    public ChatSessionContext(string chatId, RoomData roomData) :
        this(chatId, new EncryptingManager(roomData, null), new ChatSessionStarter(chatId))
    {
        
    }
    
    public ChatSessionContext(RoomInfo roomInfo) :
        this(roomInfo.ChatId, new EncryptingManager(roomInfo.Settings, null),
            new ChatSessionStarter(roomInfo.ChatId))
    {
        
    }


    public ChatSessionContext(string chatId, EncryptingManager encryptingManager, ChatSessionStarter auth)
    {
        ChatId = chatId;
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
        _auth.ResetDhState();
        await _auth.InitializeSessionAsync(externalCt);
        
        if (!_auth.IsDhComplete)
        {
            Log.Warning("Не удалось открыть сессию");
            throw new OperationCanceledException("Session was not exchanged with DH params");
        }
       
        MessageReceivingCts = new CancellationTokenSource();

       _encryptingManager.SetKey(_auth.SharedSecret);

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
            using var instance = ServerApiClient.Instance;

            using var call = instance.GrpcClient.ReceiveMessages(request, cancellationToken: ct);

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
                    await Dispatcher.UIThread.InvokeAsync(() => OnIvChanged?.Invoke(ChatId));
                    Log.Debug("Setting up new IV");
                    continue;
                }

                byte[] decryptedData;
                try
                {
                   decryptedData =  await _encryptingManager.DecryptMessage(message.Data.ToByteArray(), ct);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка расшифровки сообщения в чате {0}", ChatId);
                    await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(ChatId, new ChatMessageModel( ChatId, "Система", "[Ошибка расшифровки сообщения]", DateTime.UtcNow, false, MessageType.Message, null)));
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
                    message.Type,
                    null
                );

                if (message.Type != MessageType.Message)
                {
                    chatMessage.Filename = messageContent;
                    await Dispatcher.UIThread.InvokeAsync(() => OnFileReceived?.Invoke(message, chatMessage));
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(ChatId, chatMessage));
                }
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

    public async Task GenerateNewIv(ChatListItemModel chat)
    {
        if (!IsSubscribedToMessages)
        {
            Log.Warning("Нельзя отправить сообщение без обмена ключом");
            OnMessageError?.Invoke(ChatId, $"Нельзя отправить сообщение без обмена ключом");
            return;
        }

        try
        {
            byte[] iv = _encryptingManager.GenerateNewIv();
            using var instance = ServerApiClient.Instance;

            await instance.GrpcClient.SendMessageAsync(new Message()
            {
                ChatId = this.ChatId,
                Data = ByteString.CopyFrom(iv),
                Timestamp = DateTime.Now.ToString("o"),
                Type = MessageType.NewIv,
                FromUsername = Auth.Instance.AuthenticatedUsername!
            });
            Log.Debug("Sent new iv: {0}, to chat {1}", iv, ChatId);
            OnIvChanged?.Invoke(ChatId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось отправить сообщение {0}", ChatId);
            OnMessageError?.Invoke(ChatId, $"Внутренняя ошибка: {ex.Message}");
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
            using var instance = ServerApiClient.Instance;

            await instance.GrpcClient.SendMessageAsync(new Message()
            {
                ChatId = this.ChatId,
                Data = ByteString.CopyFrom(await _encryptingManager.EncryptMessage(content)),
                Timestamp = message.Timestamp.ToString("o"),
                Type = message.MessageType,
                FromUsername = message.Sender
            });
            Log.Debug("Sended message: {0}, to chat {1}", message.Content, ChatId);
            OnMessageReceived?.Invoke(ChatId, new ChatMessageModel( ChatId, message.Sender, message.Content, message.Timestamp, true, message.MessageType, null));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось отправить сообщение {0}", ChatId);
            OnMessageError?.Invoke(ChatId, $"Внутренняя ошибка: {ex.Message}");
        }
    }
    
    public async Task UploadAndEncryptFile(string loadPath, string filename,
        MessageType type, ProgressBar progress, CancellationToken ct = default)
    {
        if (type == MessageType.Message)
        {
            Log.Warning("Нельзя отправить файлом сообщение");
            return;
        }
        
        // TODO: много всяких проблем потенциально может быть
        
        Log.Debug("Начали отправку и шифрование файла {0}", filename);
        try
        {
            using EncryptedFileModel file = new EncryptedFileModel(loadPath);
            long size = file.GetFullSize();
            var result = await file.ReadNextFragmentAsync(ct);
            if (result == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => OnFileUploadError?.Invoke("Неподдерживаемый для отправки формат или нет доступа к файлу"));
                Log.Warning("Неподдерживаемый для отправки формат (not is FILE)");

                return;
            }
            using var instance = ServerApiClient.Instance;
            using var call = instance.GrpcClient.SendFile(cancellationToken: ct);
            
            var encryptedFilename =
                ByteString.CopyFrom(
                    await _encryptingManager.EncryptMessage(ByteString.CopyFromUtf8(filename).ToByteArray(), ct));
            
            await Dispatcher.UIThread.InvokeAsync(() => progress.Maximum = size);
            var encoder = _encryptingManager.BuildEncoder();
            while (result is not null)
            {
                byte[] data = result.Value.Data;
                long offset = result.Value.Offset;

                data = await EncryptingManager.EncryptMessageManual(encoder, data, ct);
                await Dispatcher.UIThread.InvokeAsync(() => progress.Value += data.Length);

                await call.RequestStream.WriteAsync(new FileChunk()
                {
                    ChatId = ChatId,
                    Offset = offset,
                    Data = ByteString.CopyFrom(data),
                    FileName = encryptedFilename,
                    Type = type,
                    Meta = size,
                }, ct);
                result = await file.ReadNextFragmentAsync(ct);
            }

            await call.RequestStream.CompleteAsync();
            var ack = await call.ResponseAsync;
            
            if (ack.Ok)
            {
                await Dispatcher.UIThread.InvokeAsync(() => OnMessageReceived?.Invoke(ChatId, 
                    new ChatMessageModel(ChatId,
                        Auth.Instance.AuthenticatedUsername!,
                        loadPath,
                        DateTime.Now,
                        true,
                        type,
                        filename)));
                Log.Debug("Закончили отправку и шифрование файла {0}", filename);
            }
            else
            {
                Log.Warning("На сервере возникла ошибка {0}", ack.Error);
                await Dispatcher.UIThread.InvokeAsync(() => OnFileUploadError?.Invoke("Передача была прервана с другой стороны"));
            }
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "Rpc exception during upload file");
            await Dispatcher.UIThread.InvokeAsync(() => OnFileUploadError?.Invoke("Передача была прервана по вине сервера"));

        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unknown exception during upload file");
            await Dispatcher.UIThread.InvokeAsync(() => OnFileUploadError?.Invoke("Возникла ошибка при шифровании или чтении файла"));
        }
    }

    public async Task<bool> DownloadAndDecryptFile(FileRequest request, string savePath, ProgressBar progress, CancellationToken ct = default)
    {
        // TODO: много всяких проблем потенциально может быть
        try
        {
            Log.Debug("Начали скачивание и дешифрование файла {0}", request.FileName);
            using EncryptedFileModel file = new EncryptedFileModel(savePath);
            using var instance = ServerApiClient.Instance;
            using var call = instance.GrpcClient.ReceiveFile(request, cancellationToken: ct);

            var encoder = _encryptingManager.BuildEncoder();
            await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
            {
                await Dispatcher.UIThread.InvokeAsync(() => progress.Maximum = chunk.Meta);
                byte[] data = await EncryptingManager.DecryptMessageManual(encoder, chunk.Data.ToByteArray(), ct);
                await file.AppendFragmentAtOffsetAsync(chunk.Offset, data, ct);
                await Dispatcher.UIThread.InvokeAsync(() => progress.Value += chunk.Data.Length);
            }

            Log.Debug("Закончили скачивание и дешифрование файла {0}", request.FileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unknown exception during decrypt file");
            return false;
        }
        return true;
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