using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Serilog;
using StainsGate;

namespace AvaloniaClient.Services;

public sealed class ServerApiClient : IDisposable
{
    private GrpcChannel? _channel;
    private HackingGate.HackingGateClient? _internalGrpcClient;
    private readonly string _serverAddress;
    private readonly object _lock = new object();
    private volatile bool _isConnectionIssueDetected = false;

    private static readonly Lazy<ServerApiClient> QlazyInstance =
        new(() => new ServerApiClient(Config.Instance.ServerAddress));

    public static ServerApiClient Instance => QlazyInstance.Value;

    private ServerApiClient(string serverAddress)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            throw new ArgumentNullException(nameof(serverAddress));
        }
        _serverAddress = serverAddress;
    }

    public HackingGate.HackingGateClient GrpcClient
    {
        get
        {
            EnsureClientInitialized();
            if (_internalGrpcClient == null)
            {
                throw new InvalidOperationException("gRPC client could not be initialized. Server might be unavailable.");
            }
            return _internalGrpcClient;
        }
    }

    private void EnsureClientInitialized()
    {
        if (_internalGrpcClient == null || _isConnectionIssueDetected || _channel?.State == ConnectivityState.Shutdown)
        {
            lock (_lock)
            {
                if (_internalGrpcClient == null || _isConnectionIssueDetected || _channel?.State == ConnectivityState.Shutdown)
                {
                    Log.Information("Attempting to initialize or re-initialize gRPC client for {Address} (IssueDetected: {Issue}, ChannelState: {State}).", 
                        _serverAddress, _isConnectionIssueDetected, _channel?.State);

                    DisposeCurrentChannelAndClient();

                    try
                    {
                        var httpHandler = new SocketsHttpHandler
                        {
                            EnableMultipleHttp2Connections = true,
                            ConnectTimeout = TimeSpan.FromSeconds(10),
                            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                            KeepAlivePingTimeout = TimeSpan.FromSeconds(30), 
                            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                        };
                        
                        var defaultMethodConfig = new MethodConfig
                        {
                            Names = { MethodName.Default }, 
                            RetryPolicy = new RetryPolicy
                            {
                                MaxAttempts = 4,
                                InitialBackoff = TimeSpan.FromSeconds(1),
                                MaxBackoff = TimeSpan.FromSeconds(5),
                                BackoffMultiplier = 1.5,
                                RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.DeadlineExceeded }
                            }
                        };
                        var serviceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } };

                        _channel = GrpcChannel.ForAddress(_serverAddress, new GrpcChannelOptions
                        {
                            HttpHandler = httpHandler,
                            ServiceConfig = serviceConfig,
                            // Credentials = ChannelCredentials.Insecure 
                        });

                        var invoker = _channel.Intercept(new AuthInterceptor(() => Auth.Instance.Token));
                        _internalGrpcClient = new HackingGate.HackingGateClient(invoker);
                        _isConnectionIssueDetected = false; 
                        Log.Information("gRPC client initialized successfully for {Address}.", _serverAddress);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to initialize gRPC client for {Address}.", _serverAddress);
                        _isConnectionIssueDetected = true;
                        DisposeCurrentChannelAndClient();
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Позволяет явно пометить, что возникла проблема с соединением, 
    /// чтобы следующая попытка доступа к GrpcClient принудительно переинициализировала его.
    /// Может вызываться из Interceptor'а или из кода, обрабатывающего RpcException.
    /// </summary>
    public void ReportConnectionIssue()
    {
        Log.Warning("Connection issue reported. Forcing gRPC client re-initialization on next access.");
        _isConnectionIssueDetected = true; 
        // Можно также немедленно сбросить клиент, если это необходимо:
        // lock(_lock) { DisposeCurrentChannelAndClient(); }
    }

    private void DisposeCurrentChannelAndClient()
    {
        _internalGrpcClient = null;
        _channel?.Dispose();
        _channel = null;
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
            lock (_lock)
            {
                DisposeCurrentChannelAndClient();
            }
        }
        _disposed = true;
    }

    ~ServerApiClient()
    {
        Dispose(false);
    }

    private sealed class AuthInterceptor : Interceptor 
    {
        private readonly Func<string?> _getToken;
        public AuthInterceptor(Func<string?> getToken) => _getToken = getToken;


        private AsyncUnaryCall<TResponse> HandleException<TRequest, TResponse>(AsyncUnaryCall<TResponse> call)
            where TRequest : class
            where TResponse : class
        {
            var responseAsync = call.ResponseAsync.ContinueWith(task =>
            {
                if (task.Exception != null)
                {
                    if (task.Exception.GetBaseException() is RpcException rpcEx &&
                        (rpcEx.StatusCode == StatusCode.Unavailable || rpcEx.StatusCode == StatusCode.Internal))
                    {
                        ServerApiClient.Instance.ReportConnectionIssue();
                    }
                }
                return task;
            }).Unwrap();

            return new AsyncUnaryCall<TResponse>(
                responseAsync,
                call.ResponseHeadersAsync,
                call.GetStatus,
                call.GetTrailers,
                call.Dispose);
        }

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
                // Log.Verbose("Added token for unary call {Method}", context.Method);
            }
            var opts = context.Options.WithHeaders(headers);
            var newCtx = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, opts);
            
            var call = continuation(request, newCtx);

            // return HandleException<TRequest, TResponse>(call); 
            return call;
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
            }
            var opts = context.Options.WithHeaders(headers);
            var newCtx = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, opts);
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
            }
            var opts = context.Options.WithHeaders(headers);
            var newCtx = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, opts);
            return continuation(newCtx);
        }
    }
}