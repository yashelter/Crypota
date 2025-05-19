using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Notification;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaClient.Contexts;
using AvaloniaClient.Models;
using AvaloniaClient.Repositories;
using AvaloniaClient.Services;
using AvaloniaClient.Views;
using Serilog;
using StainsGate;

namespace AvaloniaClient.ViewModels;


public partial class DashboardViewModel : ViewModelBase
{
    private readonly Action _onLogout;

    [ObservableProperty] private ObservableCollection<ChatListItemModel> _chatList;

    public INotificationMessageManager Manager { get; } = new NotificationMessageManager();

    private ToastManager _toast;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedChatMessages))]
    [NotifyPropertyChangedFor(nameof(SelectedChatIsNull))]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private ChatListItemModel? _selectedChat;
    
    private HackingGate.HackingGateClient client;

    public bool SelectedChatIsNull => _selectedChat is null;
    
    public ObservableCollection<ChatMessageModel>? SelectedChatMessages
    {
        get
        {
            if (SelectedChat == null) return null;
            return _allMessages.GetValueOrDefault(SelectedChat.Id);
        }
    }

    private Dictionary<string, ObservableCollection<ChatMessageModel>> _allMessages;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string? _newMessageText;

    [ObservableProperty] private bool _isOptionsPanelOpen = false;
    
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleSubscriptionButtonText))]
    private bool _isSubscribedToSelectedChatMessages = false;

    public string ToggleSubscriptionButtonText => IsSubscribedToSelectedChatMessages ? "Отключиться от сообщений" : "Подключиться к сообщениям";
    
    private readonly Dictionary<string, ChatSessionContext> _chatSessionContexts = new ();
    
    private readonly Auth _auth;

    public DashboardViewModel(Action onLogout)
    {
        client = ServerApiClient.Instance.Client;
        _auth = Auth.Instance;
        _onLogout = onLogout ?? throw new ArgumentNullException(nameof(onLogout));
        _chatList = new ObservableCollection<ChatListItemModel>();
        _allMessages = new Dictionary<string, ObservableCollection<ChatMessageModel>>();
        _toast = new ToastManager(Manager);
        Log.Information("DashboardViewModel: Экземпляр создан.");
        _ = LoadDataAsync();
    }
    
    
    private async Task LoadDataAsync()
    {
        using var repo = new ChatRepository();

        var chats = await Task.Run(() => repo.GetAllChats().ToList());
        var allMessages = new Dictionary<string, List<ChatMessageModel>>();

        foreach (var chatId in chats.Select(ch => ch.ChatId))
        {
            allMessages[chatId] = await Task.Run(() => repo.GetMessages(chatId).ToList());
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var chat in chats)
            {
                ChatList.Add(new ChatListItemModel(chat, DeleteChat, RequestRemoveUserFromChat));
                
                _chatSessionContexts[chat.ChatId] = new ChatSessionContext(chat.ChatId, client, chat.GetRoomData());
                InitChatSessionContext(_chatSessionContexts[chat.ChatId]);
                
                _allMessages[chat.ChatId] = new ObservableCollection<ChatMessageModel>(allMessages[chat.ChatId]);
            }
            SelectedChat = ChatList.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedChat));
        });
    }
    
    partial void OnSelectedChatChanged(ChatListItemModel? oldValue, ChatListItemModel? newValue)
    {
        if (newValue?.Id != null)
        {
            if (!_chatSessionContexts.TryGetValue(newValue.Id, out var currentChatContext))
            {
                Log.Error("OnSelectedChatChanged, not existed chatSessionContext it's serious bug");
                _toast.ShowErrorMessageToast("OnSelectedChatChanged: Not existed chatSessionContext");
                return;
                // Or throw ex: throw new NotSupportedException("")
                // It can't be possible, as after data loading is created new session context.
                // But if something is not thought good better
            }

            IsSubscribedToSelectedChatMessages = currentChatContext.MessageReceivingCts != null && !currentChatContext.MessageReceivingCts.IsCancellationRequested;
        }
        else
        {
            IsSubscribedToSelectedChatMessages = false;
        }

        OnPropertyChanged(nameof(SelectedChatMessages));
        SendMessageCommand.NotifyCanExecuteChanged();
        CopySelectedChatIdCommand.NotifyCanExecuteChanged();
        ToggleMessageSubscriptionCommand.NotifyCanExecuteChanged();
        
        Log.Debug("DashboardViewModel: Выбран чат: {0}", newValue?.Id ?? "Нет");
    }
    

    [RelayCommand(CanExecute = nameof(CanToggleMessageSubscription))]
    private async Task ToggleMessageSubscriptionAsync()
    {
        if (SelectedChat == null) return;

        var chatId = SelectedChat.Id;
        if (!_chatSessionContexts.TryGetValue(chatId, out var context))
        {
           Log.Error("[ULTRA IMPORTANT] OnSelectedChatChanged, not existed chatSessionContext, this MUST be impossible");
           return; // or ex 
           throw new NotSupportedException("OnSelectedChatChanged, not existed chatSessionContext");
           // context MUST exist
        }

        if (IsSubscribedToSelectedChatMessages)
        {
            context.CancelMessageSubscription();
            IsSubscribedToSelectedChatMessages = false;
            
            Log.Information("Подписка на сообщения для чата {0} отменена пользователем.", chatId);
            _toast.ShowSuccessMessageToast($"Отключено от сообщений чата: {SelectedChat.Id}");
        }
        else
        {
            _toast.ShowSuccessMessageToast($"Подключение к сообщениям чата: {SelectedChat.Id}...");
            
            var cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            
            var messageNotification = _toast.ShowCancelPanel(
                "Отменить подключение...",
                () => 
                {
                    Log.Debug("Пользователь нажал 'Отменить подключение' для чата {ChatId}", chatId);
                    cts.Cancel(); 
                }
            );

            try
            {
                await context.InitializeSessionAsync(token);
            }
            catch (OperationCanceledException ex)
            {
                Log.Error(ex, "Операция InitializeSessionAsync для чата {ChatId} была отменена.", chatId);
                _toast.ShowErrorMessageToast($"Подключение к чату {SelectedChat.Id} отменено.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при InitializeSessionAsync для чата {ChatId}", chatId);
                _toast.ShowErrorMessageToast($"Ошибка подключения к чату {SelectedChat.Id}.");
            }
            finally
            {
                _toast.DismissMessage(messageNotification);
                cts.Dispose();
            }
        }
    }
    
    

    [RelayCommand]
    private async Task CreateChat()
    {
        Log.Verbose("DashboardViewModel: Команда CreateChatAsync вызвана.");
        IsOptionsPanelOpen = false;

        var dialogView = new CreateChatDialogView();
        var dialogViewModel = new CreateChatDialogViewModel(dialogView.CloseDialog);
        dialogView.DataContext = dialogViewModel;

        Window? ownerWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            ownerWindow = desktopLifetime.MainWindow;
        }
        if (ownerWindow is null)
        {
            throw new NotImplementedException("other device");
        }

        var dialogSuccessful = await dialogView.ShowDialog<bool>(ownerWindow);

        if (!dialogSuccessful || dialogView.DialogResultData == null)
        {
            Log.Information("DashboardViewModel: Пользователь отменил создание чата.");
            _toast.ShowErrorMessageToast("Создание чата отменено.");
            return;
        }

        var result = dialogView.DialogResultData;
        Log.Information(
            "DashboardViewModel: Пользователь хочет создать чат с параметрами: Algo={Algo}, Mode={Mode}, Padding={Padding}",
            result.SelectedEncryptAlgo, result.SelectedEncryptMode, result.SelectedPaddingMode);

        RoomPassKey? room = null;
        RoomData? roomData = null;
        try
        {
            roomData = new RoomData()
            {
                Algo = result.SelectedEncryptAlgo,
                CipherMode = result.SelectedEncryptMode,
                Padding = result.SelectedPaddingMode,
            };
            
            room = await client.CreateRoomAsync(roomData);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не смог создать комнату");
        }

        if (room == null || roomData == null)
        {
            _toast.ShowErrorMessageToast("Создание чата не удалось");
            return;
        }

        var newChat = new ChatListItemModel(
            room.ChatId,
            Auth.Instance.AuthenticatedUsername!,
            "Чат создан, нет сообщений",
            DateTime.Now,
            roomData,
            DeleteChat,
            RequestRemoveUserFromChat
        )
        {
            OwnerName = _auth.AuthenticatedUsername!,
            CreationDate = DateTime.Now
        };
        _chatSessionContexts[room.ChatId] = new ChatSessionContext(room.ChatId, client, roomData);
        InitChatSessionContext(_chatSessionContexts[room.ChatId]);

        ChatList.Add(newChat);
        _ = Task.Run(() => AddChatToBd(new ChatModel() 
            {
                ChatId = newChat.Id,
                OwnerUsername = newChat.CreatorName,
                Algorithm = roomData.Algo,
                Padding = roomData.Padding,
                CipherMode = roomData.CipherMode
                
            }));
        
        if (!_allMessages.ContainsKey(newChat.Id))
        {
            _allMessages[newChat.Id] = new ObservableCollection<ChatMessageModel>();
        }

        SelectedChat = newChat;
        _toast.ShowSuccessMessageToast("Чат успешно создан");
    }

    private void InitChatSessionContext(ChatSessionContext ctx)
    {
        ctx.OnMessageReceived += MessageReceived;
        ctx.OnMessageError += (id, error) => _toast.ShowErrorMessageToast($"Ошибка {error}, в чате {id}");
        ctx.OnSubscriptionStarted +=(id) => _toast.ShowSuccessMessageToast($"Начался обмен сообщениями в чате {id}");

        ctx.OnFileReceived += async (message, model) =>
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            await FileReceived(message, model, cts.Token);
            
        };
        
        ctx.OnSubscriptionStarted += (id) =>
        {
            if (SelectedChat?.Id == id)
            {
                IsSubscribedToSelectedChatMessages = true;
                OnPropertyChanged(nameof(IsSubscribedToSelectedChatMessages));
            }  
        };
        
        ctx.OnSubscriptionStopped +=(id) => _toast.ShowSuccessMessageToast($"Закончился обмен сообщениями в чате {id}");
        ctx.OnSubscriptionStopped +=(id) => _chatSessionContexts[id].CancelMessageSubscription();
        ctx.OnSubscriptionStopped +=(id) =>  
        {
            if (SelectedChat?.Id == id)
            {
                IsSubscribedToSelectedChatMessages = false;
                OnPropertyChanged(nameof(IsSubscribedToSelectedChatMessages));
            }  
        };
        
        ctx.SessionStarter.OnDhCompleted +=(id) => 
            _toast.ShowSuccessMessageToast($"Успешный обмен параметрами ДХ для чата {id}");
        
        ctx.SessionStarter.OnDhError +=(id, error) => 
            _toast.ShowSuccessMessageToast($"Ошибка обмена '{error}' параметрами ДХ в чате {id}");
    }
    
    
    [RelayCommand]
    private async Task JoinChat()
    {
        Log.Information("DashboardViewModel: Команда JoinChat вызвана.");
        IsOptionsPanelOpen = false;

        var dialogView = new JoinChatDialogView();
        var dialogViewModel = new JoinChatDialogViewModel(dialogView.CloseDialog);
        dialogView.DataContext = dialogViewModel;

        Window? ownerWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            ownerWindow = desktopLifetime.MainWindow;
        }

        if (ownerWindow is null)
        {
            throw new NotImplementedException("other device");
        }

        var dialogSuccessful = await dialogView.ShowDialog<bool>(ownerWindow);
        var chatIdToJoin = dialogSuccessful ? dialogView.DialogResultChatId : null;

        if (!dialogSuccessful || string.IsNullOrWhiteSpace(chatIdToJoin)) 
        {
            Log.Information("DashboardViewModel: Пользователь отменил подключение к чату.");
            _toast.ShowErrorMessageToast("Подключение к чату отменено.");
            return;
        }

        Log.Information("DashboardViewModel: Пользователь хочет присоединиться к чату с ID: {ChatId}", chatIdToJoin);

        try
        {
            var room = await client.JoinRoomAsync(new RoomPassKey()
            {
                ChatId = chatIdToJoin,
            });
            
            var newJoinedChat = new ChatListItemModel(room.ChatId, $"{chatIdToJoin}", "Вы присоединились к чату.", 
                    DateTime.Now, room.Settings, DeleteChat, RequestRemoveUserFromChat)
                { OwnerName = $"{room.OwnerUsername}", CreationDate = DateTime.Now };

            ChatList.Add(newJoinedChat);
            _ = Task.Run(() => AddChatToBd(new ChatModel() 
            {
                ChatId = room.ChatId,
                OwnerUsername = room.OwnerUsername,
                Algorithm = room.Settings.Algo,
                Padding = room.Settings.Padding,
                CipherMode = room.Settings.CipherMode
                
            }));

            if (!_allMessages.ContainsKey(newJoinedChat.Id))
            {
                _allMessages[newJoinedChat.Id] = new ObservableCollection<ChatMessageModel>();
            }

            _chatSessionContexts[room.ChatId] = new ChatSessionContext(client, room);
            InitChatSessionContext(_chatSessionContexts[room.ChatId]);
            SelectedChat = newJoinedChat;
            
            Log.Information("DashboardViewModel: Успешно присоединились и добавили чат: {0}", chatIdToJoin);
            _toast.ShowSuccessMessageToast("Успешное присоединение к чату");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DashboardViewModel: Ошибка при попытке присоединения к чату {0}", chatIdToJoin);
            _toast.ShowErrorMessageToast("Подключение к чату отменено.");
        }
    }
    
    private bool CanSendMessage()
    {
        return SelectedChat != null && SelectedChatMessages != null && !string.IsNullOrWhiteSpace(NewMessageText);
    }
    

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessage()
    {
        if (!CanSendMessage() || SelectedChat == null || SelectedChatMessages == null) return;

        var messageContent = NewMessageText!.Trim();
        var newMessage = new ChatMessageModel(SelectedChat.Id, _auth.AuthenticatedUsername!, messageContent, DateTime.Now, true, MessageType.Message);
        await _chatSessionContexts[SelectedChat.Id].SendMessage(newMessage);
        NewMessageText = string.Empty;
        Log.Information("DashboardViewModel: Отправлено сообщение в чат '{ChatName}': {Message}", SelectedChat.Id, messageContent);
    }

    private void MessageReceived(string chatId, ChatMessageModel message)
    {
        var chatMessages = _allMessages[chatId];
        chatMessages.Add(message);
        var chat = ChatList.First(c => c.Id == chatId);
        
        chat.LastMessage = message.Content;
        chat.LastMessageTime = message.Timestamp;
        Task.Run(() => AddMessageToBd(message));
    }
    
    private async Task FileReceived(Message response, ChatMessageModel message, CancellationToken ct)
    {
        Log.Debug("FileReceived: получаем файл");
        var chatMessages = _allMessages[response.ChatId];
        var chat = ChatList.First(c => c.Id == response.ChatId);
        string savePath = Path.Combine(Config.Instance.TempPath, message.Content);
        
        
        await _chatSessionContexts[response.ChatId].DownloadAndDecryptFile(
            new FileRequest()
            {
                ChatId = response.ChatId,
                FileName = response.Data
            }, savePath, ct);
        
        _ = Task.Run(() => AddMessageToBd(message), ct);
        
        message.Content = savePath;
        chat.LastMessage = "Получен Файл";
        chat.LastMessageTime = message.Timestamp;
        
        chatMessages.Add(message);
    }


    private void AddMessageToBd(ChatMessageModel message)
    {
        using var repo = new ChatRepository();
        repo.AddMessage(message);
    }
    
    private void AddChatToBd(ChatModel chat)
    {
        using var repo = new ChatRepository();
        repo.AddChat(chat);
    }


    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];
    
    [RelayCommand]
    private async Task SelectFile()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (window is null)
        {
            Log.Debug("SelectFile: unsupported application lifetime");
            return;
        }
        
        if (SelectedChat is null || !IsSubscribedToSelectedChatMessages)
        {
            Log.Debug("Can't upload to no chat");
            _toast.ShowErrorMessageToast("Нельзя отправить файл, не начав сессию в чате");
            return;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new ("Изображения")
                {
                    Patterns =  ImageExtensions.Select(ext => $"*{ext}").ToArray()
                },
                new ("Все файлы")
                {
                    Patterns = ["*"]
                }
            }
        });

        if (files is { Count: > 0 })
        {
            var file = files[0];
            var path = file.Path.AbsolutePath;
            Log.Information("Пользователь выбрал файл: {0}", path);

            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            var isImage = ImageExtensions.Contains(ext);
            
            MessageType type  = isImage ? MessageType.Image : MessageType.File;
            await _chatSessionContexts[SelectedChat.Id].UploadAndEncryptFile(path,file.Name, type);
        }

    }


    private void DeleteChat(ChatListItemModel chatToDelete)
    {
        if (chatToDelete == null) return;

        Log.Information("DashboardViewModel: Запрос на удаление чата '{0}' (ID: {1})", chatToDelete.Id, chatToDelete.Id);

        ChatList.Remove(chatToDelete);
        if (_allMessages.Remove(chatToDelete.Id))
        {
            Log.Debug("DashboardViewModel: Сообщения для чата {0} удалены.", chatToDelete.Id);
        }

        if (SelectedChat == chatToDelete)
        {
            SelectedChat = ChatList.FirstOrDefault();
            Log.Debug("DashboardViewModel: Удален выбранный чат, выбран новый: {0}", SelectedChat?.Id ?? "Нет");
        }
        client.CloseRoomAsync(new RoomPassKey()
        {
            ChatId = chatToDelete.Id,
        });
        using var repo = new ChatRepository();
        repo.DeleteChat(chatToDelete.Id);
        
        Log.Information("DashboardViewModel: Чат '{0}' удален из списка.", chatToDelete.Id);
    }


    private void RequestRemoveUserFromChat(ChatListItemModel chatContext)
    {
        if (chatContext == null) return;

        Log.Information("DashboardViewModel: Запрос на удаление пользователя из чата '{0}' (ID: {1})", chatContext.Id, chatContext.Id);
        // TODO:
    }
    
    public void OnMessageWasClicked(ChatMessageModel msg)
    {
        // Предполагаем, что коллекция — ObservableCollection и ChatMessageModel реализует INotifyPropertyChanged
        // Тогда менять msg.Content достаточно, UI обновит автоматически
        // TODO: либо расшифровываем тут, либо тут ничего
    }

    public void OnFileWasClicked(ChatMessageModel msg)
    {
        Log.Debug("OnFileWasClicked: entering");
    }
}