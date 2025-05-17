using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using AvaloniaClient.Models;
using AvaloniaClient.Services;
using AvaloniaClient.ViewModels;
using Grpc.Core;

namespace AvaloniaClient.ViewModels;

public partial class LoginRegisterViewModel(
    Action onLoginSuccess,
    Action showLoginTabAction,
    Action showRegisterTabAction,
    AuthApiClient authApiClient,
    Auth auth)
    : ViewModelBase
{
        private readonly Action? _onLoginSuccess = onLoginSuccess ?? throw new ArgumentNullException(nameof(onLoginSuccess));
        private readonly Action? _showLoginTabAction = showLoginTabAction ?? throw new ArgumentNullException(nameof(showLoginTabAction));
        private readonly Action? _showRegisterTabAction = showRegisterTabAction ?? throw new ArgumentNullException(nameof(showRegisterTabAction));
        private readonly AuthApiClient _authApiClient = authApiClient ?? throw new ArgumentNullException(nameof(authApiClient));
        private readonly Auth _auth = auth ?? throw new ArgumentNullException(nameof(auth));

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
        private async Task LoginUser()
        {
            ErrorMessage = null;
            if (Login is null || Password is null || Login.Length < 4 || Password.Length < 4)
            {
                ErrorMessage = "Неверные данные";
                return;
            }
            
            var response = await _authApiClient.LoginAsync(Login, Password);
            
            if (response is null)
            {
                ErrorMessage = "Неверный логин или пароль.";
                return;
            }
            
            await _auth.SaveToken(response.Token);
            ClearFieldsAndError();
            
            _onLoginSuccess?.Invoke();
        }

        private bool CanRegisterUser() =>
            !string.IsNullOrWhiteSpace(Login) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(ConfirmPassword) &&
            Password == ConfirmPassword;
        
        [RelayCommand(CanExecute = nameof(CanRegisterUser))]
        private async Task RegisterUser()
        {
            ErrorMessage = null;
            if (Password == null || ConfirmPassword == null || Login == null)
            {
                ErrorMessage = "Необходимо заполнить поля";
                return;
            }
            if (Login.Length < 4)
            {
                ErrorMessage = "Логин слишком короткий";
                return;
            }
            if (Password.Length < 4)
            {
                ErrorMessage = "Пароль слишком короткий";
                return;
            }
            
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Пароли не совпадают.";
                return;
            }
            
            Console.WriteLine($"Регистрация: Логин={Login}, Пароль={Password}");

            var response = await _authApiClient.RegisterAsync(Login, Password);
            if (response is null)
            {
                ErrorMessage = "Логин Занят";
                return;
            }
            await _auth.SaveToken(response.Token);
            ClearFieldsAndError();
            
            _onLoginSuccess?.Invoke();
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
