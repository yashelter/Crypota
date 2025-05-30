using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaClient.Models;
using AvaloniaClient.Services;
using AvaloniaClient.ViewModels;
using AvaloniaClient.Views;
using Serilog;
using Serilog.Events;


namespace AvaloniaClient;

public partial class App : Application
{
    public override void Initialize()
    {
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        var logFilePath = Path.Combine(logDirectory, "AvaloniaClient_Log_.txt");
        
        Auth.Instance = Auth.CreateAsync().Result;
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        try
        {
            Log.Information("Приложение запускается. Логгер сконфигурирован.");
            AvaloniaXamlLoader.Load(this);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Критическая ошибка при инициализации приложения в App.Initialize()");
            throw;
        }
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainViewModel();

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