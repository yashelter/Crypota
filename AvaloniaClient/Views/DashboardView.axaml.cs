using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaloniaClient.ViewModels;
using Avalonia.Input;
using AvaloniaClient.Models;
using Serilog;

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
        
        private void OnMessagePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is ChatMessageModel msg)
            {
                Log.Information("Клик по сообщению: {0}", msg.Content);
                if (DataContext is DashboardViewModel vm)
                {
                    vm.OnMessageWasClicked(msg);
                }
            }
        }
        
        private async void OnFilePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control ctrl || ctrl.DataContext is not ChatMessageModel msg) return;
            if ( (DataContext is not DashboardViewModel vm)) return;
            try
            {
                await vm.OnFileWasClicked(msg);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OnFilePointerPressed: error [OnFileWasClicked]");
            }
        }
    }
}