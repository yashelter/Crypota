using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Notification;
using AvaloniaClient.Models;
using AvaloniaClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using StainsGate;

namespace AvaloniaClient.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{

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
            _toast.ShowSuccessMessageToast($"ID чата '{SelectedChat.Id}' скопирован");
        }
        else
        {
            Log.Warning("Не удалось получить доступ к буферу обмена (clipboard is null).");
            _toast.ShowErrorMessageToast("Буфер обмена недоступен.");
        }
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

    private bool CanToggleMessageSubscription()
    {
        return SelectedChat != null;
    }


    private bool CanCopySelectedChatId()
    {
        return SelectedChat != null && !string.IsNullOrEmpty(SelectedChat.Id);
    }


    public DashboardViewModel() // Конструктор для Design Time
    {
        _onLogout = () => Console.WriteLine("Дизайнер: Выход");
        _auth = Auth.Instance; 
        _chatList = new ObservableCollection<ChatListItemModel>();
        _allMessages = new Dictionary<string, ObservableCollection<ChatMessageModel>>();
        Manager = new NotificationMessageManager(); // Безопасно
        _toast = new ToastManager(Manager); // Безопасно


        _selectedChat = new ChatListItemModel("чат", "имя создателя", "пусто",
            DateTime.Now,
            new RoomData() { Algo = EncryptAlgo.Rc6, Padding = PaddingMode.Ansix923, CipherMode = EncryptMode.Rd },
            (_) => { }, (_) => { });
        if (_selectedChat != null && !_allMessages.ContainsKey(_selectedChat.Id))
        {
            _allMessages[_selectedChat.Id] = new ObservableCollection<ChatMessageModel>
            {
                new ChatMessageModel(_selectedChat.Id, "Дизайнер", "Сообщение для превью", DateTime.Now, false, MessageType.Message)
            };
        }

        IsSubscribedToSelectedChatMessages = true;

        _chatList.Add(new ChatListItemModel("Чат 1 (дизайн)", "Тест", "пусто", DateTime.Now,
            new RoomData() { Algo = EncryptAlgo.Rc6, Padding = PaddingMode.Ansix923, CipherMode = EncryptMode.Rd },
            (_) => { }, (_) => { }));

        Log.Information("DashboardViewModel: Экземпляр создан для XAML дизайнера.");
    }
}
