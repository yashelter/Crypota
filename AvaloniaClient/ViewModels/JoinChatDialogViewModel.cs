// Файл: ViewModels/JoinChatDialogViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace AvaloniaClient.ViewModels;

public partial class JoinChatDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmJoinCommand))]
    private string? _chatIdInput;

    public string? EnteredChatId { get; private set; }

    // Делегат для закрытия окна с результатом (true - подтверждено, false - отменено)
    private readonly Action<bool>? _closeAction;

    public JoinChatDialogViewModel(Action<bool> closeAction)
    {
        _closeAction = closeAction;
    }

    // Конструктор для XAML-дизайнера (если окно будет открываться в дизайнере)
    public JoinChatDialogViewModel() : this(_ => { }) { }


    private bool CanConfirmJoin()
    {
        // Простая проверка, что что-то введено. Можно добавить валидацию GUID.
        return !string.IsNullOrWhiteSpace(ChatIdInput);
    }

    [RelayCommand(CanExecute = nameof(CanConfirmJoin))]
    private void ConfirmJoin()
    {
        EnteredChatId = ChatIdInput;
        _closeAction?.Invoke(true); // Закрыть окно с результатом "успех"
    }

    [RelayCommand]
    private void Cancel()
    {
        _closeAction?.Invoke(false); // Закрыть окно с результатом "отмена"
    }
}