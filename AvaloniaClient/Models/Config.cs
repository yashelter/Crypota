using System;
using System.IO;
using System.Text.Json;

public class Config
{
    private static readonly Lazy<Config> _instance = new(LoadConfig);

    public static Config Instance => _instance.Value;

    public string ServerAddress { get; init; }
    public string MongoConnectionString { get; init; }
    public string MongoDatabase { get; init; }

    public string RedisConnectionString { get; init; }
    
    private static Config LoadConfig()
    {
        const string configFileName = "config.json";

        if (!File.Exists(configFileName))
            throw new FileNotFoundException($"Файл конфигурации не найден: {configFileName}");

        var json = File.ReadAllText(configFileName);
        var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? throw new InvalidOperationException("Не удалось десериализовать конфиг");
    }
}