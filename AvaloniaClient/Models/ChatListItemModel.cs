using System;
using AvaloniaClient.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaClient.Models;

public partial class ChatListItemModel : ViewModelBase
{
    public string Id { get; }
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _lastMessage;
    [ObservableProperty] private DateTime _lastMessageTime;

    // Новые свойства для отображения в контекстном меню
    [ObservableProperty] private string _ownerName = "Система"; // Пример
    [ObservableProperty] private DateTime _creationDate = DateTime.UtcNow; // Пример

    // Делегаты для действий, которые будут предоставлены DashboardViewModel
    private readonly Action<ChatListItemModel>? _deleteChatAction;
    private readonly Action<ChatListItemModel>? _requestRemoveUserAction;

    public IRelayCommand DeleteChatCommand { get; }
    public IRelayCommand RequestRemoveUserCommand { get; }

    public ChatListItemModel(
        string id,
        string name,
        string lastMessage,
        DateTime lastMessageTime,
        Action<ChatListItemModel> deleteChatAction,
        Action<ChatListItemModel> requestRemoveUserAction)
    {
        Id = id;
        _name = name;
        _lastMessage = lastMessage;
        _lastMessageTime = lastMessageTime;

        _deleteChatAction = deleteChatAction;
        _requestRemoveUserAction = requestRemoveUserAction;

        DeleteChatCommand = new RelayCommand(ExecuteDeleteChat);
        RequestRemoveUserCommand = new RelayCommand(ExecuteRequestRemoveUser);
    }
    

    private void ExecuteDeleteChat()
    {
        _deleteChatAction?.Invoke(this);
    }

    private void ExecuteRequestRemoveUser()
    {
        // Здесь можно добавить логику, если ChatListItemViewModel должен что-то сделать перед вызовом
        // например, собрать какую-то дополнительную информацию.
        _requestRemoveUserAction?.Invoke(this);
    }
}