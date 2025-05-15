using Avalonia.Controls;
using AvaloniaClient.ViewModels; 

namespace AvaloniaClient.Views
{
    public partial class JoinChatDialogView : Window
    {
        public string? DialogResultChatId { get; private set; }

        public JoinChatDialogView()
        {
            InitializeComponent();
        }

        public void CloseDialog(bool dialogResult)
        {
            if (dialogResult && DataContext is JoinChatDialogViewModel vm)
            {
                DialogResultChatId = vm.EnteredChatId;
            }
            Close(dialogResult);
        }
    }
}