using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Notification;
using AvaloniaClient.Models;
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
            // TODO: загружать из бд
            return _allMessages.GetValueOrDefault(SelectedChat.Id);
        }
    }

    private Dictionary<string, ObservableCollection<ChatMessageModel>> _allMessages;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string? _newMessageText;

    [ObservableProperty] private bool _isOptionsPanelOpen = false;
    
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleSubscriptionButtonText))]
    private bool _isSubscribedToSelectedChatMessages = false; // TODO: set up auto this

    public string ToggleSubscriptionButtonText => IsSubscribedToSelectedChatMessages ? "Отключиться от сообщений" : "Подключиться к сообщениям";
    
    private readonly Dictionary<string, ChatSessionContext> _chatSessionContexts = new Dictionary<string, ChatSessionContext>();
    
    private readonly Auth _auth;

    public DashboardViewModel(Action onLogout)
    {
        client = ServerApiClient.Instance._client;
        _auth = Auth.Instance;
        _onLogout = onLogout ?? throw new ArgumentNullException(nameof(onLogout));
        _chatList = new ObservableCollection<ChatListItemModel>();
        _allMessages = new Dictionary<string, ObservableCollection<ChatMessageModel>>();
        _toast = new ToastManager(Manager);
        Log.Information("DashboardViewModel: Экземпляр создан.");
        LoadData();
    }


    private void LoadData()
    {
        // TODO
    }
    
    partial void OnSelectedChatChanged(ChatListItemModel? oldValue, ChatListItemModel? newValue)
    {
        if (oldValue?.Id != null && _chatSessionContexts.TryGetValue(oldValue.Id, out var oldContext))
        {
            if (!(oldContext.MessageReceivingCts?.IsCancellationRequested ?? true))
            {
                 oldContext.MessageReceivingCts?.Cancel();
                 Log.Debug("Подписка на сообщения для чата {0} отменена при смене чата.", oldValue.Id);
            }
        }

        if (newValue?.Id != null)
        {
            if (!_chatSessionContexts.ContainsKey(newValue.Id))
            {
                /*_chatSessionContexts[newValue.Id] = new ChatSessionContext(room.ChatId, client, roomData);
                _chatSessionContexts[room.ChatId].OnMessageReceived += MessageReceived;*/
                Log.Error("OnSelectedChatChanged, not existed chatSessionContext it's requires rejoin ");
                // TODO: 
            }
            var currentChatContext = _chatSessionContexts[newValue.Id];
            
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
        
        Log.Debug("DashboardViewModel: Выбран чат: {0}", newValue?.Name ?? "Нет");
    }

    [RelayCommand(CanExecute = nameof(CanToggleMessageSubscription))]
    private async Task ToggleMessageSubscriptionAsync()
    {
        if (SelectedChat == null) return;

        var chatId = SelectedChat.Id;
        if (!_chatSessionContexts.TryGetValue(chatId, out var context))
        {
           Log.Error("OnSelectedChatChanged, not existed chatSessionContext, this MUST be impossible");
           throw new NotSupportedException("OnSelectedChatChanged, not existed chatSessionContext");
           // context MUST exist
        }

        if (IsSubscribedToSelectedChatMessages)
        {
            context.CancelMessageSubscription();
            IsSubscribedToSelectedChatMessages = false;
            
            Log.Information("Подписка на сообщения для чата {0} отменена пользователем.", chatId);
            _toast.ShowSuccessMessageToast($"Отключено от сообщений чата: {SelectedChat.Name}");
        }
        else
        {
            _toast.ShowSuccessMessageToast($"Подключение к сообщениям чата: {SelectedChat.Name}...");
            await context.InitializeSessionAsync();
        }
    }

    private void OnSubscriptionStopped(string chatId)
    {
        
    }

    private bool CanToggleMessageSubscription()
    {
        return SelectedChat != null;
    }


    [RelayCommand(CanExecute = nameof(CanCopySelectedChatId))]
    private async Task CopySelectedChatIdAsync()
    {
        if (SelectedChat?.Id == null)
        {
            Log.Warning("Не удалось скопировать ID чата: чат не выбран.");
            return;
        }

        TopLevel? topLevel = null;
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
        {
            topLevel = desktopApp.MainWindow; 
        }

        if (topLevel == null)
        {
            Log.Error("Не удалось получить TopLevel для доступа к буферу обмена.");
            _toast.ShowErrorMessageToast("Не удалось получить доступ к буферу обмена.");
            return;
        }
        
        var clipboard = topLevel.Clipboard;

        if (clipboard != null)
        {
            await clipboard.SetTextAsync(SelectedChat.Id);
            Log.Information("ID чата {ChatId} скопирован в буфер обмена.", SelectedChat.Id);
            _toast.ShowSuccessMessageToast($"ID чата '{SelectedChat.Name}' скопирован");
        }
        else
        {
            Log.Warning("Не удалось получить доступ к буферу обмена (clipboard is null).");
            _toast.ShowErrorMessageToast("Буфер обмена недоступен.");
        }
    }
    
    
    private bool CanCopySelectedChatId()
    {
        return SelectedChat != null && !string.IsNullOrEmpty(SelectedChat.Id);
    }

    [RelayCommand]
    private void Logout()
    {
        Log.Information("DashboardViewModel: Команда Logout вызвана.");
        _onLogout.Invoke();
    }

    [RelayCommand]
    private void ToggleOptionsPanel()
    {
        IsOptionsPanelOpen = !IsOptionsPanelOpen;
        Log.Debug("DashboardViewModel: Панель опций переключена, новое состояние: {IsOpen}", IsOptionsPanelOpen);
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
            room.ChatId,
            "Чат создан, нет сообщений",
            DateTime.Now,
            result.SelectedEncryptAlgo,
            result.SelectedEncryptMode,
            result.SelectedPaddingMode,
            DeleteChat,
            RequestRemoveUserFromChat
        )
        {
            OwnerName = _auth.AuthenticatedUsername!,
            CreationDate = DateTime.Now
        };
        _chatSessionContexts[room.ChatId] = new ChatSessionContext(room.ChatId, client, roomData);
        _chatSessionContexts[room.ChatId].OnMessageReceived += MessageReceived;

        ChatList.Add(newChat);
        if (!_allMessages.ContainsKey(newChat.Id)) // TODO
        {
            _allMessages[newChat.Id] = new ObservableCollection<ChatMessageModel>();
        }

        SelectedChat = newChat;
        _toast.ShowSuccessMessageToast("Чат успешно создан");
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
            
            var newJoinedChat = new ChatListItemModel(
                    room.ChatId,
                    $"{chatIdToJoin}",
                    "Вы присоединились к чату.",
                    DateTime.Now,
                    room.Settings.Algo,
                    room.Settings.CipherMode,
                    room.Settings.Padding,
                    DeleteChat,
                    RequestRemoveUserFromChat
                )
                { OwnerName = $"{room.OwnerUsername}", CreationDate = DateTime.Now };

            ChatList.Add(newJoinedChat); // TODO
            if (!_allMessages.ContainsKey(newJoinedChat.Id))
            {
                _allMessages[newJoinedChat.Id] = new ObservableCollection<ChatMessageModel>();
            }

            _chatSessionContexts[room.ChatId] = new ChatSessionContext(client, room);
            _chatSessionContexts[room.ChatId].OnMessageReceived += MessageReceived;
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
        var newMessage = new ChatMessageModel(_auth.AuthenticatedUsername!, messageContent, DateTime.Now, true, MessageType.Message);
        await _chatSessionContexts[SelectedChat.Id].SendMessage(newMessage);
        NewMessageText = string.Empty;
        Log.Information("DashboardViewModel: Отправлено сообщение в чат '{ChatName}': {Message}", SelectedChat.Name, messageContent);
    }

    private void MessageReceived(string chatId, ChatMessageModel message)
    {
        var chatMessages = _allMessages[chatId];
        chatMessages.Add(message);
        var chat = ChatList.First(c => c.Id == chatId);
        
        chat.LastMessage = message.Content;
        chat.LastMessageTime = message.Timestamp;
        
    }

    [RelayCommand]
    private void SelectFile()
    {
        Log.Information("DashboardViewModel: Запрос выбора файла (логика не реализована).");
    }
    
    
     private void DeleteChat(ChatListItemModel chatToDelete)
    {
        if (chatToDelete == null) return;

        Log.Information("DashboardViewModel: Запрос на удаление чата '{0}' (ID: {1})", chatToDelete.Name, chatToDelete.Id);

        ChatList.Remove(chatToDelete);
        if (_allMessages.ContainsKey(chatToDelete.Id))
        {
            _allMessages.Remove(chatToDelete.Id);
            Log.Debug("DashboardViewModel: Сообщения для чата {0} удалены.", chatToDelete.Id);
        }

        if (SelectedChat == chatToDelete)
        {
            SelectedChat = ChatList.FirstOrDefault();
            Log.Debug("DashboardViewModel: Удален выбранный чат, выбран новый: {0}", SelectedChat?.Name ?? "Нет");
        }
        client.CloseRoomAsync(new RoomPassKey()
        {
            ChatId = chatToDelete.Id,
        });
        Log.Information("DashboardViewModel: Чат '{0}' удален из списка.", chatToDelete.Name);
    }


    private void RequestRemoveUserFromChat(ChatListItemModel chatContext)
    {
        if (chatContext == null) return;

        Log.Information("DashboardViewModel: Запрос на удаление пользователя из чата '{0}' (ID: {1})", chatContext.Name, chatContext.Id);

        // TODO: Реализовать логику отображения диалогового окна
        // 1. Создать ViewModel для диалога удаления пользователя (например, RemoveUserDialogViewModel)
        // 2. Передать в него chatContext.Id и, возможно, список пользователей этого чата
        // 3. Отобразить диалоговое окно (модальное)
        // 4. Если пользователь подтверждает удаление и выбирает пользователя:
        //    - Вызвать API сервера для удаления пользователя из чата
        //    - Обновить локальные данные, если нужно
    }
}