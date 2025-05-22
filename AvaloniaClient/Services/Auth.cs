using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Data;
using AvaloniaClient.Contexts;
using Serilog;

namespace AvaloniaClient.Services;

public class Auth
{
    private readonly string _tokenSavePath = Path.Combine(AppContext.BaseDirectory, "../auth.token");
    
    public string? Token { get; private set; }
    public string? AuthenticatedUsername { get; private set; }

    public static Auth Instance { get; set; }
    

    private Auth() { }
    
    public static async Task<Auth> CreateAsync()
    {
        var inst = new Auth();
        await inst.LoadToken().ConfigureAwait(false);
        return inst;
    }
    
    /// <summary>
    /// Проверяет токен и извлекает имя пользователя.
    /// Возвращает Optional с именем пользователя, если токен действителен.
    /// </summary>
    public Optional<string> GetValidUsername()
    {
        if (string.IsNullOrEmpty(Token))
        {
            AuthenticatedUsername = null;
            return Optional<string>.Empty;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(Token);

            if (jwt.ValidTo > DateTime.UtcNow)
            {
                var usernameClaim = jwt.Claims
                    .FirstOrDefault(c =>
                        c.Type == JwtRegisteredClaimNames.UniqueName ||
                        c.Type == JwtRegisteredClaimNames.Sub);
                
                if (usernameClaim != null && !string.IsNullOrEmpty(usernameClaim.Value))
                {
                    AuthenticatedUsername = usernameClaim.Value;
                    return new Optional<string>(AuthenticatedUsername);
                }
                else
                {
                    Log.Warning("Токен действителен, но claim имени пользователя (ClaimTypes.Name) не найден или пуст.");
                    AuthenticatedUsername = null;
                    return Optional<string>.Empty;
                }
            }
            else
            {
                Log.Information("Токен истек: ValidTo = {ValidTo}", jwt.ValidTo);
                AuthenticatedUsername = null;
                DeleteToken();
                return Optional<string>.Empty;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при чтении или валидации JWT токена.");
            AuthenticatedUsername = null;
            DeleteToken();
            return Optional<string>.Empty;
        }
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
        GetValidUsername();

        var result =  jwt.ValidTo > DateTime.UtcNow
            ? new Optional<string>(Token)
            : Optional<string>.Empty;

        if (!result.HasValue)
        {
            LiteDbContext.ClearDb();
        }
        return result;
    }


    public async Task SaveToken(string token)
    {
        LiteDbContext.ClearDb();
        await File.WriteAllTextAsync(_tokenSavePath, token).ConfigureAwait(false);
        Token = token;
        GetValidUsername();
    }

    
    public void DeleteToken()
    {
        Token = null;
        AuthenticatedUsername = null;
        File.Delete(_tokenSavePath);
        LiteDbContext.ClearDb();
    }
    

    private async Task LoadToken()
    {
        if (File.Exists(_tokenSavePath))
        {
            try
            {
                Token = await File.ReadAllTextAsync(_tokenSavePath).ConfigureAwait(false);
                Log.Information("Токен загружен из файла.");
                GetValidUsername();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при загрузке токена из файла.");
                Token = null;
                AuthenticatedUsername = null;
                LiteDbContext.ClearDb();
            }
        }
        else
        {
            Log.Information("Файл токена не найден.");
            Token = null;
            AuthenticatedUsername = null;
            LiteDbContext.ClearDb();
        }
        
    }
}