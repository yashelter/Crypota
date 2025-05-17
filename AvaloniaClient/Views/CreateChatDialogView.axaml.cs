using Avalonia.Controls;
using AvaloniaClient.ViewModels;

namespace AvaloniaClient.Views;

public partial class CreateChatDialogView : Window
{
    public CreateChatDialogResult? DialogResultData { get; private set; }

    public CreateChatDialogView()
    {
        InitializeComponent();
    }

    public void CloseDialog(bool dialogResult)
    {
        if (dialogResult && DataContext is CreateChatDialogViewModel vm)
        {
            DialogResultData = vm.DialogResult;
        }
        Close(dialogResult);
    }
}