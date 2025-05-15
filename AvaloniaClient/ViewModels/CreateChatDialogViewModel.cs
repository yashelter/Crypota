using StainsGate;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvaloniaClient.ViewModels;

// Класс для хранения результата диалога
public class CreateChatDialogResult
{
    public EncryptAlgo SelectedEncryptAlgo { get; set; }
    public EncryptMode SelectedEncryptMode { get; set; }
    public PaddingMode SelectedPaddingMode { get; set; }
}

public partial class CreateChatDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private EncryptAlgo _selectedEncryptAlgo;

    [ObservableProperty]
    private EncryptMode _selectedEncryptMode;

    [ObservableProperty]
    private PaddingMode _selectedPaddingMode;

    public IEnumerable<EncryptAlgo> EncryptAlgos => Enum.GetValues(typeof(EncryptAlgo)).Cast<EncryptAlgo>();
    public IEnumerable<EncryptMode> EncryptModes => Enum.GetValues(typeof(EncryptMode)).Cast<EncryptMode>();
    public IEnumerable<PaddingMode> PaddingModes => Enum.GetValues(typeof(PaddingMode)).Cast<PaddingMode>();

    public CreateChatDialogResult? DialogResult { get; private set; }

    private readonly Action<bool> _closeAction; // Делегат для закрытия окна

    public CreateChatDialogViewModel(Action<bool> closeAction)
    {
        _closeAction = closeAction;

        SelectedEncryptAlgo = EncryptAlgos.FirstOrDefault();
        SelectedEncryptMode = EncryptModes.FirstOrDefault();
        SelectedPaddingMode = PaddingModes.FirstOrDefault();
    }

    // Конструктор для XAML-дизайнера
    public CreateChatDialogViewModel() : this(_ => { }) { }



    [RelayCommand]
    private void ConfirmCreate()
    {
        DialogResult = new CreateChatDialogResult
        {
            SelectedEncryptAlgo = SelectedEncryptAlgo,
            SelectedEncryptMode = SelectedEncryptMode,
            SelectedPaddingMode = SelectedPaddingMode
        };
        _closeAction?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        _closeAction?.Invoke(false);
    }
}