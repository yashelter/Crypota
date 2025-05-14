// App.axaml.cs

using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaClient.ViewModels;
using AvaloniaClient.Views;
// Добавьте using
// Добавьте using

// Для Task

namespace AvaloniaClient
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted() // Сделаем метод async void
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainViewModel = new MainViewModel(/* здесь можно передать сервисы */);

                await mainViewModel.InitializeAsync();

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime _)
            {
                throw new NotImplementedException("Not supported yet");
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}