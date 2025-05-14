using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Avalonia.Controls;
using AvaloniaClient.ViewModels;

namespace AvaloniaClient.ViewModels;

public partial class LoginRegisterViewModel : ViewModelBase 
    {
        private readonly Action? _onLoginSuccess;
        private readonly Action? _showLoginTabAction;
        private readonly Action? _showRegisterTabAction;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterUserCommand))]
        private string? _login;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterUserCommand))]
        private string? _password;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterUserCommand))]
        private string? _confirmPassword;

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LoginModeText))]
        [NotifyPropertyChangedFor(nameof(ActionButtonContent))]
        [NotifyCanExecuteChangedFor(nameof(LoginUserCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterUserCommand))]
        private bool _isLoginMode = true;

        public string LoginModeText => IsLoginMode ? "Вход в систему" : "Регистрация";
        public string ActionButtonContent => IsLoginMode ? "Войти" : "Зарегистрироваться";



        // Конструктор для использования во время выполнения (runtime)
        public LoginRegisterViewModel(Action onLoginSuccess, Action showLoginTabAction, Action showRegisterTabAction)
        {
            _onLoginSuccess = onLoginSuccess ?? throw new ArgumentNullException(nameof(onLoginSuccess));
            _showLoginTabAction = showLoginTabAction ?? throw new ArgumentNullException(nameof(showLoginTabAction));
            _showRegisterTabAction = showRegisterTabAction ?? throw new ArgumentNullException(nameof(showRegisterTabAction));
        }

        // Конструктор без параметров для XAML-дизайнера
        public LoginRegisterViewModel()
        {
            // Этот конструктор будет вызван дизайнером.
            // Можно оставить зависимости null или инициализировать заглушками, если это нужно для отображения в дизайнере.
            // _onLoginSuccess = () => Console.WriteLine("Дизайнер: Успешный вход");
            // _showLoginTabAction = () => Console.WriteLine("Дизайнер: Показать вкладку входа");
            // _showRegisterTabAction = () => Console.WriteLine("Дизайнер: Показать вкладку регистрации");

            // Для простоты можно оставить их null, если логика команд это допускает или не используется в дизайнере.
             _onLoginSuccess = null;
             _showLoginTabAction = null;
             _showRegisterTabAction = null;

            // Можно также инициализировать некоторые свойства для лучшего отображения в дизайнере
            // Login = "TestUser";
            // ErrorMessage = "Пример ошибки для дизайнера";
            // IsLoginMode = false; // Например, чтобы протестировать вид регистрации
        }

        [RelayCommand]
        private void ShowLoginTab()
        {
            IsLoginMode = true;
            ClearFieldsAndError();
            _showLoginTabAction?.Invoke();
        }

        [RelayCommand]
        private void ShowRegisterTab()
        {
            IsLoginMode = false;
            ClearFieldsAndError();
            _showRegisterTabAction?.Invoke();
        }

        
        
        [RelayCommand]
        public void LoginOrRegisterUser()
        {
            if (IsLoginMode)
            {
                LoginUser();
            }
            else
            {
                RegisterUser();
            }
        }
        
        private bool CanLoginUser() => !string.IsNullOrWhiteSpace(Login) && !string.IsNullOrWhiteSpace(Password);
        
        [RelayCommand(CanExecute = nameof(CanLoginUser))]
        private void LoginUser()
        {
            ErrorMessage = null;
            if (Login == "admin" && Password == "password")
            {
                _onLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = "Неверный логин или пароль.";
            }
        }

        private bool CanRegisterUser() =>
            !string.IsNullOrWhiteSpace(Login) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(ConfirmPassword) &&
            Password == ConfirmPassword;
        
        [RelayCommand(CanExecute = nameof(CanRegisterUser))]
        private void RegisterUser()
        {
            ErrorMessage = null;
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Пароли не совпадают.";
                return;
            }
            Console.WriteLine($"Регистрация: Логин={Login}, Пароль={Password}");
            IsLoginMode = true;
            ClearFields();
            ErrorMessage = "Регистрация успешна! Теперь вы можете войти.";
        }

        public void ClearFields()
        {
            Login = null;
            Password = null;
            ConfirmPassword = null;
        }
        
        public void ClearFieldsAndError()
        {
            ClearFields();
            ErrorMessage = null;
        }
    }
