using System;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaClient.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly Action? _onLogout;

    public DashboardViewModel(Action onLogout)
    {
        _onLogout = onLogout ?? throw new ArgumentNullException(nameof(onLogout));
    }

    // Конструктор без параметров для XAML-дизайнера
    public DashboardViewModel()
    {
        // _onLogout = () => Console.WriteLine("Дизайнер: Выход");
        _onLogout = null;
    }

    [RelayCommand]
    private void Logout()
    {
        _onLogout?.Invoke();
    }
}