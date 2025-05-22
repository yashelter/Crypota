using System;
using System.Threading.Tasks;
using AvaloniaClient.Models; // Предполагается, что Config здесь или доступен
using Grpc.Core;
using Grpc.Net.Client;
using Serilog;
using StainsGate; // Пространство имен для Authentication.AuthenticationClient

namespace AvaloniaClient.Services;

public class AuthApiClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly Authentication.AuthenticationClient _client;

    private static readonly Lazy<AuthApiClient> _lazyInstance =
        new Lazy<AuthApiClient>(() => new AuthApiClient(Config.Instance.ServerAddress));

    public static AuthApiClient Instance => _lazyInstance.Value;


    private AuthApiClient(string serverAddress)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            Log.Error("AuthApiClient: ServerAddress is null or whitespace during construction.");
            throw new ArgumentNullException(nameof(serverAddress));
        }
        Log.Information("AuthApiClient: Initializing with server address {ServerAddress}", serverAddress);
        
        // Убираем using, канал теперь - поле класса
        _channel = GrpcChannel.ForAddress(serverAddress);
        _client = new Authentication.AuthenticationClient(_channel);
        Log.Information("AuthApiClient: GrpcChannel and AuthenticationClient created.");
    }

    public async Task<AuthResponse?> RegisterAsync(string login, string password)
    {
        var request = new RegisterRequestData
        {
            Username = login,
            PasswordHash = password // TODO: hash this password before sending
        };
        Log.Debug("AuthApiClient: RegisterAsync called for user {Username}", login);
        try
        {
            var response = await _client.RegisterAsync(request);
            Log.Information("AuthApiClient: Registration successful for user {Username}", login);
            return response;
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "AuthApiClient: gRPC error during registration for user {Username}. Status: {StatusCode}, Detail: {Detail}", login, ex.StatusCode, ex.Status.Detail);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AuthApiClient: Generic error during registration for user {Username}", login);
            return null;
        }
    }

    public async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        var request = new LoginRequestData
        {
            Username = username,
            PasswordHash = password // TODO: hash this password before sending
        };
        Log.Debug("AuthApiClient: LoginAsync called for user {Username}", username);
        try
        {
            var response = await _client.LoginAsync(request);
            Log.Information("AuthApiClient: Login successful for user {Username}", username);
            return response;
        }
        catch (RpcException ex)
        {
            Log.Error(ex, "AuthApiClient: gRPC error during login for user {Username}. Status: {StatusCode}, Detail: {Detail}", username, ex.StatusCode, ex.Status.Detail);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AuthApiClient: Generic error during login for user {Username}", username);
            return null;
        }
    }

    private bool _disposed = false;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Освобождаем управляемые ресурсы
            Log.Debug("AuthApiClient: Disposing GrpcChannel.");
            _channel?.Dispose();
        }

        _disposed = true;
    }

    ~AuthApiClient()
    {
        Dispose(false);
    }
}