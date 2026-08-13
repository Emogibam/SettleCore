using SettleCore.Api.Middleware;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConfiguration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
    return ConnectionMultiplexer.Connect(redisConfiguration);
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<IdempotencyMiddleware>();
app.MapOpenApi();


app.UseHttpsRedirection();


app.Run();

