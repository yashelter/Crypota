using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StainsGate;
using static StainsGate.Authentication;

namespace Server.Services;

public class AuthService : AuthenticationBase
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override Task<AuthResponse> Register(RegisterRequestData request, ServerCallContext context)
    {
        // TODO: save user credentials (e.g., hash password) in persistent store
        // For demo, skip persistence
        var token = GenerateJwt(request.Username);
        var expiresIn = int.Parse(_configuration["Jwt:ExpiresInSeconds"]);
        return Task.FromResult(new AuthResponse { Token = token, ExpiresIn = expiresIn });
    }

    public override Task<AuthResponse> Login(LoginRequestData request, ServerCallContext context)
    {
        // TODO: validate credentials against store
        // For demo, accept any
        var token = GenerateJwt(request.Username);
        var expiresIn = int.Parse(_configuration["Jwt:ExpiresInSeconds"]);
        return Task.FromResult(new AuthResponse { Token = token, ExpiresIn = expiresIn });
    }

    public override Task<ValidateResponse> ValidateToken(ValidateRequest request, ServerCallContext context)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]);
        try
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
            var principal = handler.ValidateToken(request.Token, parameters, out var validatedToken);
            var username = principal.Identity.Name;
            var exp = validatedToken.ValidTo;
            var expiresIn = (long)(exp - DateTime.UtcNow).TotalSeconds;
            return Task.FromResult(new ValidateResponse { Valid = true, Username = username, ExpiresIn = expiresIn });
        }
        catch
        {
            return Task.FromResult(new ValidateResponse { Valid = false });
        }
    }

    private string GenerateJwt(string username)
    {
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]);
        var tokenHandler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
            Expires = DateTime.UtcNow.AddSeconds(int.Parse(_configuration["Jwt:ExpiresInSeconds"])),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(descriptor);
        return tokenHandler.WriteToken(token);
    }
}
