using System;
using System.Threading.Tasks;
using AvaloniaClient.Models;
using AvaloniaClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaClient.ViewModels;

 public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase? _currentViewModel;

        private readonly LoginRegisterViewModel _loginRegisterViewModel;
        private readonly DashboardViewModel _dashboardViewModel;
        

        private readonly Auth _authService;
        private readonly AuthApiClient _authApiClient;

        public MainViewModel()
        {
            var authService = Auth.Instance;
            var authApiClient = AuthApiClient.Instance;
            
            _authService = authService ?? throw new ArgumentException(nameof(authService));
            _authApiClient = authApiClient ?? throw new ArgumentException(nameof(authApiClient));
            
            _loginRegisterViewModel = new LoginRegisterViewModel(GoToDashboard, ShowLogin, ShowRegister, 
                authApiClient, authService);
            _dashboardViewModel = new DashboardViewModel(GoToLoginRegister);
        }
        

        public async Task InitializeAsync()
        {
            var token = _authService.CanEnter();
            if (!token.HasValue)
            {
                CurrentViewModel = _loginRegisterViewModel;
                _loginRegisterViewModel.IsLoginMode = true;
            }
            else
            {
                CurrentViewModel = _dashboardViewModel;
                // TODO: validate
            }
        }


        private void GoToDashboard()
        {
            CurrentViewModel = _dashboardViewModel;
        }

        private void GoToLoginRegister()
        {
            CurrentViewModel = _loginRegisterViewModel;
            _authService.DeleteToken();
            _loginRegisterViewModel.ClearFieldsAndError();
            _loginRegisterViewModel.IsLoginMode = true;
        }

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