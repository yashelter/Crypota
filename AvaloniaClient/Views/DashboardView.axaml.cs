using Avalonia.Controls;
using AvaloniaClient.ViewModels;
using Avalonia.Input;

namespace AvaloniaClient.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var textBox = sender as TextBox;
                if (textBox != null && DataContext is DashboardViewModel vm)
                {
                    // Если нажат Shift+Enter, то переносим строку
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        var caretIndex = textBox.CaretIndex;
                        textBox.Text = textBox.Text?.Insert(caretIndex, System.Environment.NewLine);
                        textBox.CaretIndex = caretIndex + System.Environment.NewLine.Length;
                        e.Handled = true;
                    }
                    else
                    {
                        if (vm.SendMessageCommand.CanExecute(null))
                        {
                            vm.SendMessageCommand.Execute(null);
                        }
                        e.Handled = true;
                    }
                }
            }
        }
    }
}