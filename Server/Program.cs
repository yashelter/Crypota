using StackExchange.Redis;
using MongoDB.Driver;
using Server.Services;
using Server.Models;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(0);


// MongoDB
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(config["Mongo:ConnectionString"]));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(config["Mongo:Database"]));


// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(config["Redis:ConnectionString"]
                                  ?? throw new InvalidOperationException("Missing Redis config")));


builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<JwtServerInterceptor>();
});

builder.Services.AddSingleton<IConstraint, Constraints>();

builder.Services.AddSingleton<DhStateStore>();
builder.Services.AddSingleton<SessionStore>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HackingGateService>();

var app = builder.Build();

app.MapGrpcService<AuthService>();
app.MapGrpcService<HackingGateService>();

app.MapGet("/", () => "nya");
app.Run();