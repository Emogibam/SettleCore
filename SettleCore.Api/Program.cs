using Hangfire;
using Hangfire.PostgreSql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SettleCore.Api.Middleware;
using SettleCore.Core.Saga;
using SettleCore.Infrastructure.Consumers;
using SettleCore.Infrastructure.Data;
using SettleCore.Infrastructure.Services;
using SettleCore.Infrastructure.StateMachines;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

// 1. Redis Registration
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConfiguration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
    return ConnectionMultiplexer.Connect(redisConfiguration);
});

// 2. PostgreSQL + EF Core Registration
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("PostgreSql")
    ?? "Host=localhost;Port=5432;Database=SettleCoreDb;Username=settlecore;Password=password123";

builder.Services.AddDbContext<SettleCoreDbContext>(options =>
{
    options.UseNpgsql(defaultConnection, b => b.MigrationsAssembly("SettleCore.Infrastructure"));
});

// 3. MassTransit + RabbitMQ + Saga Registration
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<TransferRequestedConsumer>();
    x.AddConsumer<ReverseSenderDebitConsumer>();
    x.AddConsumer<NotifyUserConsumer>();

    // Register TransferSagaStateMachine with EF Core PostgreSQL persistence
    x.AddSagaStateMachine<TransferSagaStateMachine, TransferState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<SettleCoreDbContext>();
            r.UsePostgres();
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        // Automatically configure endpoints for registered consumers and sagas
        cfg.ConfigureEndpoints(context);
    });
});

// 4. Hangfire Async Polling Engine Registration
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(defaultConnection)));

builder.Services.AddHangfireServer();

// 5. Mock NIP Bridge HttpClient & Reconciliation Service Registration
var mockBridgeBaseUrl = builder.Configuration["MockNipBridge:BaseUrl"] ?? "http://localhost:5246";

builder.Services.AddHttpClient("MockNipBridge", client =>
{
    client.BaseAddress = new Uri(mockBridgeBaseUrl);
});

builder.Services.AddHttpClient<INipReconciliationService, NipReconciliationService>(client =>
{
    client.BaseAddress = new Uri(mockBridgeBaseUrl);
});

builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<IdempotencyMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOpenApi();
app.MapScalarApiReference();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "SettleCore API v1");
    options.RoutePrefix = "swagger";
});

// Expose Hangfire Dashboard
app.UseHangfireDashboard("/hangfire");

// Expose SignalR Notification Hub
app.MapHub<SettleCore.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.MapGet("/swagger", () => Results.Redirect("/swagger/index.html"));

// Automatically apply pending database migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SettleCoreDbContext>();
    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database migration on startup was unable to reach PostgreSQL: {Message}", ex.Message);
    }
}

app.Run();

