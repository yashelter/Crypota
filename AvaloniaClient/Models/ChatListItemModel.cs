using System;
using AvaloniaClient.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StainsGate;

namespace AvaloniaClient.Models;

public partial class ChatListItemModel : ViewModelBase
{
    public string Id { get; }
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _lastMessage;
    [ObservableProperty] private DateTime _lastMessageTime;

    [ObservableProperty] private string _ownerName = "None";
    [ObservableProperty] private DateTime _creationDate = DateTime.UtcNow;

    private readonly Action<ChatListItemModel>? _deleteChatAction;
    private readonly Action<ChatListItemModel>? _requestRemoveUserAction;
    
    public EncryptAlgo Algorithm { get; set; }
    public EncryptMode Mode { get; set; }
    public PaddingMode ChatPadding { get; set; } 

    public IRelayCommand DeleteChatCommand { get; }
    public IRelayCommand RequestRemoveUserCommand { get; }

    public ChatListItemModel(
        string id,
        string name,
        string lastMessage,
        DateTime lastMessageTime,
        RoomData settings,
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
        
        Algorithm = settings.Algo;
        Mode = settings.CipherMode;
        ChatPadding = settings.Padding; 
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
    
    public string AlgorithmDisplay => Algorithm.ToString();
    public string ModeDisplay => Mode.ToString();
    public string PaddingDisplay => ChatPadding.ToString();
}