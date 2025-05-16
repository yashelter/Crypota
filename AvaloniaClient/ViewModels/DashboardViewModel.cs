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
    private bool _isSubscribedToSelectedChatMessages = false;

    public string ToggleSubscriptionButtonText => IsSubscribedToSelectedChatMessages ? "Отключиться от сообщений" : "Подключиться к сообщениям";
    
    private readonly Dictionary<string, ChatSessionContext> _chatSessionContexts = new Dictionary<string, ChatSessionContext>();

    public DashboardViewModel(Action onLogout)
    {
        client = ServerApiClient.Instance._client;
        _onLogout = onLogout ?? throw new ArgumentNullException(nameof(onLogout));
        _chatList = new ObservableCollection<ChatListItemModel>();
        _allMessages = new Dictionary<string, ObservableCollection<ChatMessageModel>>();
        _toast = new ToastManager(Manager);
        Log.Information("DashboardViewModel: Экземпляр создан.");
        LoadData();
    }


    private void LoadData()
    {

    }
    
    partial void OnSelectedChatChanged(ChatListItemModel? oldValue, ChatListItemModel? newValue)
    {
        if (oldValue?.Id != null && _chatSessionContexts.TryGetValue(oldValue.Id, out var oldContext))
        {
            if (!(oldContext.MessageReceivingCts?.IsCancellationRequested ?? true))
            {
                 oldContext.MessageReceivingCts?.Cancel();
                 Log.Debug("Подписка на сообщения для чата {ChatId} отменена при смене чата.", oldValue.Id);
            }
        }

        if (newValue?.Id != null)
        {
            if (!_chatSessionContexts.ContainsKey(newValue.Id))
            {
                _chatSessionContexts[newValue.Id] = new ChatSessionContext();
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
            context = new ChatSessionContext();
            _chatSessionContexts[chatId] = context;
        }

        if (IsSubscribedToSelectedChatMessages)
        {
            context.MessageReceivingCts?.Cancel();
            IsSubscribedToSelectedChatMessages = false;
            Log.Information("Подписка на сообщения для чата {0} отменена пользователем.", chatId);
            _toast.ShowSuccessMessageToast($"Отключено от сообщений чата: {SelectedChat.Name}");
        }
        else
        {
            _toast.ShowSuccessMessageToast($"Подключение к сообщениям чата: {SelectedChat.Name}...");
            //await EnsureSecureConnectionAndSubscriptionAsync(chatId, context);
            
            IsSubscribedToSelectedChatMessages = context.IsDhComplete && (context.MessageReceivingCts != null && !context.MessageReceivingCts.IsCancellationRequested);
            if(IsSubscribedToSelectedChatMessages)
            {
                _toast.ShowSuccessMessageToast($"Подключено к сообщениям чата: {SelectedChat.Name}");
            }
            else
            {
                _toast.ShowErrorMessageToast($"Не удалось подключиться к сообщениям чата: {SelectedChat.Name}");
            }
        }
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
        else
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
        try
        {
            room = await client.CreateRoomAsync(new RoomData()
            {
                Algo = result.SelectedEncryptAlgo,
                CipherMode = result.SelectedEncryptMode,
                Padding = result.SelectedPaddingMode,
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не смог создать комнату");
        }

        if (room == null)
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
            OwnerName = "Я",
            CreationDate = DateTime.Now
        };

        ChatList.Add(newChat);
        if (!_allMessages.ContainsKey(newChat.Id))
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
        IsOptionsPanelOpen = false; // Закрыть панель опций, если открыта

        var dialogView = new JoinChatDialogView();
        var dialogViewModel = new JoinChatDialogViewModel(dialogView.CloseDialog); // Передаем метод закрытия из View
        dialogView.DataContext = dialogViewModel;

        // Находим родительское окно для модального диалога
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

            ChatList.Add(newJoinedChat);
            if (!_allMessages.ContainsKey(newJoinedChat.Id))
            {
                _allMessages[newJoinedChat.Id] = new ObservableCollection<ChatMessageModel>();
                // Добавить приветственное сообщение
                _allMessages[newJoinedChat.Id].Add(new ChatMessageModel("Система",
                    "Вы присоединились к этому чату.", DateTime.Now, false));
            }

            SelectedChat = newJoinedChat;
            Log.Information("DashboardViewModel: Успешно (локально) присоединились и добавили чат: {0}",
                chatIdToJoin);
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
    private void SendMessage()
    {
        if (!CanSendMessage() || SelectedChat == null || SelectedChatMessages == null) return;

        var messageContent = NewMessageText!.Trim();
        var newMessage = new ChatMessageModel("Я", messageContent, DateTime.Now, true);
        
        SelectedChatMessages.Add(newMessage);

        SelectedChat.LastMessage = messageContent;
        SelectedChat.LastMessageTime = newMessage.Timestamp;

        NewMessageText = string.Empty;
        Log.Information("DashboardViewModel: Отправлено сообщение в чат '{ChatName}': {Message}", SelectedChat.Name, messageContent);
    }

    [RelayCommand]
    private void SelectFile()
    {
        Log.Information("DashboardViewModel: Запрос выбора файла (логика не реализована).");
    }
    
    
     private void DeleteChat(ChatListItemModel chatToDelete)
    {
        if (chatToDelete == null) return;

        Log.Information("DashboardViewModel: Запрос на удаление чата '{ChatName}' (ID: {ChatId})", chatToDelete.Name, chatToDelete.Id);

        ChatList.Remove(chatToDelete);
        if (_allMessages.ContainsKey(chatToDelete.Id))
        {
            _allMessages.Remove(chatToDelete.Id);
            Log.Debug("DashboardViewModel: Сообщения для чата {ChatId} удалены.", chatToDelete.Id);
        }

        if (SelectedChat == chatToDelete)
        {
            SelectedChat = ChatList.FirstOrDefault();
            Log.Debug("DashboardViewModel: Удален выбранный чат, выбран новый: {NewSelectedChatName}", SelectedChat?.Name ?? "Нет");
        }
        client.CloseRoomAsync(new RoomPassKey()
        {
            ChatId = chatToDelete.Id,
        });
        Log.Information("DashboardViewModel: Чат '{ChatName}' удален из списка.", chatToDelete.Name);
    }


    private void RequestRemoveUserFromChat(ChatListItemModel chatContext)
    {
        if (chatContext == null) return;

        Log.Information("DashboardViewModel: Запрос на удаление пользователя из чата '{ChatName}' (ID: {ChatId})", chatContext.Name, chatContext.Id);

        // TODO: Реализовать логику отображения диалогового окна
        // 1. Создать ViewModel для диалога удаления пользователя (например, RemoveUserDialogViewModel)
        // 2. Передать в него chatContext.Id и, возможно, список пользователей этого чата
        // 3. Отобразить диалоговое окно (модальное)
        // 4. Если пользователь подтверждает удаление и выбирает пользователя:
        //    - Вызвать API сервера для удаления пользователя из чата
        //    - Обновить локальные данные, если нужно
    }
}