using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Server.Models;
using StackExchange.Redis;
using StainsGate;


namespace Server.Services;

public class AuthService : Authentication.AuthenticationBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IDatabase _cache;
    private readonly IConfiguration _config;

    public AuthService(IConfiguration config, IConnectionMultiplexer redis)
    {
        _config = config;
        var client = new MongoClient(_config["Mongo:ConnectionString"]);
        var db = client.GetDatabase(_config["Mongo:Database"]);
        _users = db.GetCollection<User>(_config["Mongo:UsersCollection"]);
        _cache = redis.GetDatabase();
    }

    public override async Task<AuthResponse> Register(RegisterRequestData request, ServerCallContext context)
    {
        var exists = await _users.Find(u => u.Username == request.Username).AnyAsync();
        if (exists) throw new RpcException(new Status(StatusCode.AlreadyExists, "User exists"));

        var user = new User
        {
            Username = request.Username,
            PasswordHash = request.PasswordHash
        };
        await _users.InsertOneAsync(user);

        var token = GenerateJwt(request.Username);
        var expire = int.Parse(_config["Jwt:ExpiresInSeconds"]?? 
                               throw new InvalidOperationException("Not correct settings setup"));
        await _cache.StringSetAsync(GenerateRedisKey(token), "active", TimeSpan.FromSeconds(expire));

        return new AuthResponse { Token = token, ExpiresIn = expire };
    }

    public override async Task<AuthResponse> Login(LoginRequestData request, ServerCallContext context)
    {
        var user = await _users.Find(u => u.Username == request.Username).FirstOrDefaultAsync();
        if (user == null || !request.PasswordHash.Equals(user.PasswordHash))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid credentials"));

        var token = GenerateJwt(request.Username);
        var expire = int.Parse(_config["Jwt:ExpiresInSeconds"]?? 
                               throw new InvalidOperationException("Not correct settings setup"));
        await _cache.StringSetAsync(GenerateRedisKey(token), "active", TimeSpan.FromSeconds(expire));
        return new AuthResponse { Token = token, ExpiresIn = expire };
    }

    
    public override async Task<ValidateResponse> ValidateToken(ValidateRequest req, ServerCallContext ctx)
    {
        var isActive = await _cache.KeyExistsAsync(GenerateRedisKey(req.Token));
        if (!isActive) return new ValidateResponse { Valid = false };

        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Secret"]?? 
                                         throw new InvalidOperationException("Not correct settings setup"));
        try
        {
            var prm = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            var principal = handler.ValidateToken(req.Token, prm, out var vt);
            var username = principal.Identity!.Name;
            var expiresIn = (long)(vt.ValidTo - DateTime.UtcNow).TotalSeconds;
            return new ValidateResponse { Valid = true, Username = username, ExpiresIn = expiresIn };
        }
        catch
        {
            return new ValidateResponse { Valid = false };
        }
    }

    private static string GenerateRedisKey(string jwt) => $"auth:token:{jwt.Substring(0, 8)}"; // Краткий ключ для Redis

    private string GenerateJwt(string username)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Secret"]?? 
                                         throw new InvalidOperationException("Not correct settings setup"));
        var desc = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
            Expires = DateTime.UtcNow.AddSeconds(int.Parse(_config["Jwt:ExpiresInSeconds"] ?? 
                                                           throw new InvalidOperationException("Not correct settings setup"))),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256)
        };
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityTokenHandler().CreateToken(desc));
    }
}