using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Data;

namespace AvaloniaClient.Models;

public class Auth
{
    private readonly string _tokenSavePath = Path.Combine(AppContext.BaseDirectory, "auth.token");
    
    public string? Token { get; private set; }

    public static Auth Instance { get; private set; }

    static Auth()
    {
        Instance = new Auth();
    }

    private Auth()
    {
        LoadToken().Wait();
    }

    /// <summary>
    /// Возвращает Optional
    /// - Some(token), если токен загружен и ещё действителен (ValidTo > UtcNow)
    /// - None, если токена нет или он просрочен.
    /// </summary>
    public Optional<string> CanEnter()
    {
        if (string.IsNullOrEmpty(Token))
            return Optional<string>.Empty;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(Token);

        return jwt.ValidTo > DateTime.UtcNow
            ? new Optional<string>(Token)
            : Optional<string>.Empty;
    }


    public async Task SaveToken(string token)
    {
        Token = token;
        await File.WriteAllTextAsync(_tokenSavePath, token)
            .ConfigureAwait(false);
    }

    
    public void DeleteToken()
    {
        Token = null;
        File.Delete(_tokenSavePath);
    }
    

    private async Task LoadToken()
    {
        if (File.Exists(_tokenSavePath))
        {
            Token = await File.ReadAllTextAsync(_tokenSavePath)
                .ConfigureAwait(false);
        }
    }
}