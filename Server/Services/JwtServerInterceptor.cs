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

    public JwtServerInterceptor(IConfiguration cfg, IConnectionMultiplexer redis,  ILogger<JwtServerInterceptor> logger)
    {
        _cfg = cfg;
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest,TResponse>(
        TRequest req,
        ServerCallContext ctx,
        UnaryServerMethod<TRequest,TResponse> next)
    {
        var m = ctx.Method; // e.g. "/auth.Authentication/Register"
        
        _logger.LogTrace("Called Jwt handler before method: {Method}", m);
        
        
        if (m.EndsWith("Register") ||         // TODO: to global settings (or reflection?)
            m.EndsWith("Login")    ||
            m.EndsWith("ValidateToken"))
        {
            _logger.LogTrace("{Method} was skipped by jwt handler", m);

            return await next(req, ctx);
        }
        
        var auth = ctx.RequestHeaders.GetValue("authorization");
        if (auth == null || !auth.StartsWith("Bearer "))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Token missing"));

        var token = auth.Substring("Bearer ".Length);
        // Проверяем «alive» в Redis
        if (!await _redis.KeyExistsAsync($"auth:token:{token[..8]}"))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Token revoked"));

        // Верификация подписи и срока
        var handler = new JwtSecurityTokenHandler();
        
        ClaimsPrincipal principal;
        try
        {
            var key = Encoding.UTF8.GetBytes(_cfg["Jwt:Secret"] ?? 
                                             throw new InvalidOperationException("Invalid settings setup"));
            principal = handler.ValidateToken(token, new TokenValidationParameters {
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
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Username claim missing in token."));
        }
        _logger.LogTrace("User {Username} added to config", usernameClaim.Value);
        
        ctx.UserState["username"] = usernameClaim.Value;
        
        return await next(req, ctx);
    }
}
