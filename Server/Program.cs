using StackExchange.Redis;
using MongoDB.Driver;
using Server.Services;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(0);


// MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(config["Mongo:ConnectionString"]));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(config["Mongo:Database"]));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(config["Redis:ConnectionString"]
                                  ?? throw new InvalidOperationException("Missing Redis config")));

// Подключаем глобальный JWT‑интерсептор для всех gRPC-сервисов
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<JwtServerInterceptor>();
});

// Регистрируем сервисы
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HackingGateService>();

var app = builder.Build();

// AuthService без токена (Register/Login/ValidateToken пропустят интерсептор по фильтру методов)
app.MapGrpcService<AuthService>();

// HackingService под защитой JWT
app.MapGrpcService<HackingGateService>();

app.MapGet("/", () => "gRPC server is running");
app.Run();