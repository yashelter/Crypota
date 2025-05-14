using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaClient.ViewModels;

 public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase? _currentViewModel; // Текущий отображаемый ViewModel

        private readonly LoginRegisterViewModel _loginRegisterViewModel;
        private readonly DashboardViewModel _dashboardViewModel; // Пока заглушка

        // Сервис для начальной проверки (будет добавлен позже)
        // private readonly IAuthService _authService;

        public MainViewModel(/* IAuthService authService */)
        {
            // _authService = authService;
            _loginRegisterViewModel = new LoginRegisterViewModel(GoToDashboard, ShowLogin, ShowRegister);
            _dashboardViewModel = new DashboardViewModel(GoToLoginRegister); // Передаем действие для выхода

            // Инициализация будет позже, когда добавим сервис
            // InitializeAsync();
            CurrentViewModel = _loginRegisterViewModel; // Начинаем с экрана входа/регистрации
            _loginRegisterViewModel.IsLoginMode = true; // По умолчанию режим входа
        }

        // Этот метод будет вызываться из App.axaml.cs или сервиса
        public async Task InitializeAsync()
        {
            // Здесь будет логика проверки:
            // if (await _authService.IsUserAuthenticatedAsync())
            // {
            //     CurrentViewModel = _dashboardViewModel;
            // }
            // else
            // {
            //     CurrentViewModel = _loginRegisterViewModel;
            //     _loginRegisterViewModel.IsLoginMode = true; // Убедимся, что режим "Вход"
            // }

            // Пока что просто для демонстрации
           // await Task.Delay(100); // Имитация асинхронной проверки
            bool isAuthenticated = false; // Замените на реальную проверку

            if (isAuthenticated)
            {
                CurrentViewModel = _dashboardViewModel;
            }
            else
            {
                CurrentViewModel = _loginRegisterViewModel;
                _loginRegisterViewModel.IsLoginMode = true;
            }
        }


        private void GoToDashboard()
        {
            CurrentViewModel = _dashboardViewModel;
        }

        private void GoToLoginRegister()
        {
            CurrentViewModel = _loginRegisterViewModel;
            _loginRegisterViewModel.ClearFields(); // Очищаем поля при возврате
            _loginRegisterViewModel.IsLoginMode = true; // Возвращаемся в режим входа
        }

        // Методы для переключения режимов в LoginRegisterViewModel
        private void ShowLogin()
        {
            if (CurrentViewModel is LoginRegisterViewModel lrvm)
            {
                lrvm.IsLoginMode = true;
            }
        }

        private void ShowRegister()
        {
            if (CurrentViewModel is LoginRegisterViewModel lrvm)
            {
                lrvm.IsLoginMode = false;
            }
        }
    }