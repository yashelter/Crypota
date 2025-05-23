using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Server.Services;

public class JwtServerInterceptor : Interceptor
{
    private readonly ILogger<JwtServerInterceptor> _logger;
    private readonly IConfiguration _cfg;
    private readonly IDatabase _redis;

    public JwtServerInterceptor(IConfiguration cfg, IConnectionMultiplexer redis, ILogger<JwtServerInterceptor> logger)
    {
        _cfg = cfg;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    // Unary RPC
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest req,
        ServerCallContext ctx,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        return await HandleAuth(ctx, () => base.UnaryServerHandler(req, ctx, continuation));
    }

    // Client-streaming RPC
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext ctx,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        return await HandleAuth(ctx, () => base.ClientStreamingServerHandler(requestStream, ctx, continuation));
    }

    // Server-streaming RPC
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest req,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext ctx,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await HandleAuth(ctx, async () =>
        {
            await base.ServerStreamingServerHandler(req, responseStream, ctx, continuation);
            return true;
        });
    }

    // Bi-directional streaming RPC
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext ctx,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await HandleAuth(ctx, async () =>
        {
            await base.DuplexStreamingServerHandler(requestStream, responseStream, ctx, continuation);
            return true;
        });
    }

    // Common authentication logic wrapper
    private async Task<T> HandleAuth<T>(ServerCallContext ctx, Func<Task<T>> callContinuation)
    {
        var m = ctx.Method;
        _logger.LogTrace("Called Jwt handler before method: {Method}", m);

        if (m.EndsWith("Register") || m.EndsWith("Login") || m.EndsWith("ValidateToken"))
        {
            _logger.LogTrace("{Method} was skipped by jwt handler", m);
            return await callContinuation();
        }

        var auth = ctx.RequestHeaders.GetValue("authorization");
        if (auth == null || !auth.StartsWith("Bearer "))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Token missing"));

        var token = auth.Substring("Bearer ".Length);
        if (!await _redis.KeyExistsAsync($"auth:token:{token[..8]}") )
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Token revoked"));

        var handler = new JwtSecurityTokenHandler();
        ClaimsPrincipal principal;
        try
        {
            var key = Encoding.UTF8.GetBytes(_cfg["Jwt:Secret"]
                                             ?? throw new InvalidOperationException("Invalid settings setup"));
            principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidateIssuer = false,
                ValidateAudience = false
            }, out _);
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }

        var usernameClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        if (usernameClaim == null)
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Username claim missing in token."));

        _logger.LogTrace("User {Username} added to UserState", usernameClaim.Value);
        ctx.UserState["username"] = usernameClaim.Value;

        return await callContinuation();
    }
}