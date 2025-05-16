using System;
using AvaloniaClient.Models; // Убедитесь, что Config и Auth доступны
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using StainsGate; // Пространство имен для HackingGateClient

namespace AvaloniaClient.Services;

public class ServerApiClient : IDisposable // Добавляем IDisposable
{
    private readonly GrpcChannel _channel; // Делаем канал полем
    public readonly HackingGate.HackingGateClient _client;
    
    // Ленивая инициализация для синглтона, чтобы избежать проблем с порядком инициализации статических полей
    private static readonly Lazy<ServerApiClient> _lazyInstance =
        new Lazy<ServerApiClient>(() => new ServerApiClient(Config.Instance.ServerAddress));

    public static ServerApiClient Instance => _lazyInstance.Value;
    
    // Конструктор теперь private, чтобы принудительно использовать Instance
    private ServerApiClient(string serverAddress)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            throw new ArgumentNullException(nameof(serverAddress));
        }
        
        // Убираем using, канал теперь - поле класса
        _channel = GrpcChannel.ForAddress(serverAddress); 
        var invoker = _channel.Intercept(new AuthInterceptor(() => Auth.Instance.Token));
        
        _client = new HackingGate.HackingGateClient(invoker);
    }
    
    private class AuthInterceptor : Interceptor
    {
        private readonly Func<string?> _getToken;
        public AuthInterceptor(Func<string?> getToken) => _getToken = getToken;

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest                      request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var headers = context.Options.Headers ?? new Metadata();
            var token   = _getToken();
            if (!string.IsNullOrEmpty(token))
            {
                headers.Add("authorization", $"Bearer {token}");
            }
            var opts = context.Options.WithHeaders(headers);
            var newCtx = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method, context.Host, opts);
            return continuation(request, newCtx);
        }
    }

    // Реализация IDisposable
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
            _channel?.Dispose();
        }

        _disposed = true;
    }


    ~ServerApiClient()
    {
        Dispose(false);
    }
}