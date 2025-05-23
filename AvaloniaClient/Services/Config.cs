using System;
using System.IO;
using System.Text.Json;

namespace AvaloniaClient.Services;

public class Config
{
    private static readonly Lazy<Config> _instance = new(LoadConfig);

    public static Config Instance => _instance.Value;

    public string ServerAddress { get; init; }
    public string AppDataBase { get; init; }
    public string TempPath { get; init; }

    
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
        
        if (config == null) throw new InvalidOperationException("Не удалось десериализовать конфиг");
        
        if (!Directory.Exists(config.TempPath))
        {
            Directory.CreateDirectory(config.TempPath);
        }

        return config;
    }
}