using System;
using System.Threading.Tasks;
using AvaloniaClient.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StainsGate;

namespace AvaloniaClient.Models;

public partial class ChatListItemModel : ViewModelBase
{
    public string Id { get; }
    [ObservableProperty] private string _lastMessage;
    [ObservableProperty] private DateTime _lastMessageTime;

    [ObservableProperty] private string _creatorName;
    [ObservableProperty] private DateTime _creationDate = DateTime.UtcNow;
    
    [ObservableProperty] private string _mateName;


    public required Action<ChatListItemModel> DeleteChatAction { get; init; }
    public required Action<ChatListItemModel> RequestRemoveUserAction { get; init; }
    public required Action<ChatListItemModel> RequestChangeInitialVector { get; init; }
    
    public EncryptAlgo Algorithm { get; set; }
    public EncryptMode Mode { get; set; }
    public PaddingMode ChatPadding { get; set; } 

    public IRelayCommand DeleteChatCommand { get; }
    public IRelayCommand RequestRemoveUserCommand { get; }
    public IRelayCommand ChangeIvCommand { get; }
    
    public ChatListItemModel(
        string id,
        string creatorName,
        string lastMessage,
        string mate,
        DateTime lastMessageTime,
        RoomData settings)
    {
        Id = id;
        _creatorName = creatorName;
        _lastMessage = lastMessage;
        _mateName = mate;
        _lastMessageTime = lastMessageTime;

        DeleteChatCommand = new RelayCommand(ExecuteDeleteChat);
        RequestRemoveUserCommand = new RelayCommand(ExecuteRequestRemoveUser);
        ChangeIvCommand = new RelayCommand(ExecuteChangeIvCommand);
        
        Algorithm = settings.Algo;
        Mode = settings.CipherMode;
        ChatPadding = settings.Padding; 
    }
    

    public ChatListItemModel(ChatModel chat)
    : this(chat.ChatId,
        chat.OwnerUsername,
        "Чат был загружен",
        chat.MateName,
        DateTime.Now,
        new RoomData()
        {
            Algo = chat.Algorithm,
            CipherMode = chat.CipherMode,
            Padding = chat.Padding
        })
    {
        _creationDate = chat.CreatedAt;
    }


    private void ExecuteChangeIvCommand()
    {
        RequestChangeInitialVector?.Invoke(this);
    }
    

    private void ExecuteDeleteChat()
    {
        DeleteChatAction?.Invoke(this);
    }

    private void ExecuteRequestRemoveUser()
    {
        RequestRemoveUserAction?.Invoke(this);
    }
    
    public string AlgorithmDisplay => Algorithm.ToString();
    public string ModeDisplay => Mode.ToString();
    public string PaddingDisplay => ChatPadding.ToString();
    public string ChatMate => MateName;
}