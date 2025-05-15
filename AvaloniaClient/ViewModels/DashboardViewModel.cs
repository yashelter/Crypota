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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedChatMessages))]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private ChatListItemModel? _selectedChat;
    
    private HackingGate.HackingGateClient client;
    
    public ObservableCollection<ChatMessageModel>? SelectedChatMessages
    {
        get
        {
            if (SelectedChat == null) return null;
            // TODO: загружать из бд
            return _allMessages.TryGetValue(SelectedChat.Id, out var messages) ? messages : null;
        }
    }

    private Dictionary<string, ObservableCollection<ChatMessageModel>> _allMessages;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string? _newMessageText;

    [ObservableProperty] private bool _isOptionsPanelOpen = false;

    public DashboardViewModel(Action onLogout)
    {
        client = ServerApiClient.Instance._client;
        _onLogout = onLogout ?? throw new ArgumentNullException(nameof(onLogout));
        _chatList = new ObservableCollection<ChatListItemModel>();
        _allMessages = new Dictionary<string, ObservableCollection<ChatMessageModel>>();
        Log.Information("DashboardViewModel: Экземпляр создан.");
        LoadData();
    }


    private void LoadData()
    {

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
            Manager?.CreateMessage()
                .Accent("#D32F2F")
                .Animates(true)
                .Background("#333")
                .HasBadge("Отмена")
                .HasMessage("Создание чата отменено.")
                .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
                .Queue();
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
            Manager?.CreateMessage()
                .Accent("#D32F2F")
                .Animates(true)
                .Background("#333")
                .HasBadge("Отмена")
                .HasMessage("Создание чата не удалось.")
                .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
                .Queue();
            return;
        }
        

        var newChat = new ChatListItemModel(
            room.ChatId,
            room.ChatId,
            "Чат создан, нет сообщений",
            DateTime.Now,
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

        Manager?.CreateMessage()
            .Accent("#1751C3")
            .Animates(true)
            .Background("#333")
            .HasBadge("Успех")
            .HasMessage($"Чат успешно создан!")
            .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
            .Queue();
        Log.Information("DashboardViewModel: Создан чат '{ChatName}'.");

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
            Manager?.CreateMessage()
                .Accent("#D32F2F")
                .Animates(true)
                .Background("#333")
                .HasBadge("Отмена")
                .HasMessage("Подключение к чату отменено.")
                .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
                .Queue();
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
            Log.Information("DashboardViewModel: Успешно (локально) присоединились и добавили чат: {ChatId}",
                chatIdToJoin);
            Manager?.CreateMessage()
                .Accent("#1751C3")
                .Animates(true)
                .Background("#333")
                .HasBadge("Успех")
                .HasMessage($"Успешное присоединение к чату")
                .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
                .Queue();

        }
        catch (Exception ex)
        {
            Log.Error(ex, "DashboardViewModel: Ошибка при попытке присоединения к чату {ChatId}", chatIdToJoin);
            Manager?.CreateMessage()
                .Accent("#D32F2F")
                .Animates(true)
                .Background("#333")
                .HasBadge("Отмена")
                .HasMessage("Подключение к чату отменено.")
                .Dismiss().WithDelay(TimeSpan.FromSeconds(3))
                .Queue(); 
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
        
        // SelectedChatMessages уже является ObservableCollection, поэтому добавление в нее
        // автоматически обновит UI для текущего списка сообщений.
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

    // Этот метод вызывается автоматически при изменении SelectedChat
    // благодаря атрибуту [ObservableProperty] и [NotifyPropertyChangedFor]
    partial void OnSelectedChatChanged(ChatListItemModel? oldValue, ChatListItemModel? newValue)
    {
        // OnPropertyChanged(nameof(SelectedChatMessages)); // Это уже указано в [NotifyPropertyChangedFor(nameof(SelectedChatMessages))]
        // SendMessageCommand.NotifyCanExecuteChanged();     // Это уже указано в [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))] для SelectedChat
        
        // Однако, CanSendMessage зависит еще и от SelectedChatMessages, которое может быть null
        // если для нового чата нет записи в _allMessages (хотя по текущей логике всегда есть).
        // На всякий случай, можно явно обновить состояние команды здесь, если SelectedChatMessages меняется.
        if ( (oldValue != null && _allMessages.ContainsKey(oldValue.Id)) != (newValue != null && _allMessages.ContainsKey(newValue.Id)) )
        {
             OnPropertyChanged(nameof(SelectedChatMessages)); // Убедимся, что свойство обновилось
        }
        SendMessageCommand.NotifyCanExecuteChanged(); // Убедимся, что состояние кнопки обновилось
        
        Log.Debug("DashboardViewModel: Выбран чат: {ChatName}", newValue?.Name ?? "Нет");
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

    // Метод для инициации удаления пользователя из чата
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