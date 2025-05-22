using System;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Serilog;
using StainsGate;

namespace AvaloniaClient.Services;

public sealed class ServerApiClient : IDisposable
{
    private readonly GrpcChannel _channel;
    public readonly HackingGate.HackingGateClient Client;
    
    private static readonly Lazy<ServerApiClient> QlazyInstance =
        new (() => new ServerApiClient(Config.Instance.ServerAddress));

    public static ServerApiClient Instance => QlazyInstance.Value;
    
    private ServerApiClient(string serverAddress)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            throw new ArgumentNullException(nameof(serverAddress));
        }
        
        _channel = GrpcChannel.ForAddress(serverAddress); 
        var invoker = _channel.Intercept(new AuthInterceptor(() => Auth.Instance.Token));
        
        Client = new HackingGate.HackingGateClient(invoker);
    }
    
    private sealed class AuthInterceptor : Interceptor
{
    private readonly Func<string?> _getToken;
    public AuthInterceptor(Func<string?> getToken) => _getToken = getToken;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        var token = _getToken();
        if (!string.IsNullOrEmpty(token))
        {
            headers.Add("authorization", $"Bearer {token}");
            Log.Verbose("Added token for unary call {Method}", context.Method);
        }
        else
        {
            Log.Verbose("No token for unary call {Method}", context.Method);
        }
        var opts = context.Options.WithHeaders(headers);
        var newCtx = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, opts);
        return continuation(request, newCtx);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        var token = _getToken();
        if (!string.IsNullOrEmpty(token))
        {
            headers.Add("authorization", $"Bearer {token}");
            Log.Verbose("Added token for client streaming {Method}", context.Method);
        }
        var opts = context.Options.WithHeaders(headers);
        var newCtx = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, opts);
        return continuation(newCtx);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        var token = _getToken();
        if (!string.IsNullOrEmpty(token))
        {
            headers.Add("authorization", $"Bearer {token}");
            Log.Verbose("Added token for server streaming {Method}", context.Method);
        }
        var opts = context.Options.WithHeaders(headers);
        var newCtx = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, opts);
        return continuation(request, newCtx);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        var token = _getToken();
        if (!string.IsNullOrEmpty(token))
        {
            headers.Add("authorization", $"Bearer {token}");
            Log.Verbose("Added token for duplex streaming {Method}", context.Method);
        }
        var opts = context.Options.WithHeaders(headers);
        var newCtx = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, opts);
        return continuation(newCtx);
    }
}

    private bool _disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _channel.Dispose();
        }

        _disposed = true;
    }


    ~ServerApiClient()
    {
        Dispose(false);
    }
}