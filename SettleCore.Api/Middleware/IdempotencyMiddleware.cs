using StackExchange.Redis;

namespace SettleCore.Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDatabase _redis;

    public IdempotencyMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redis = redis.GetDatabase();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != HttpMethods.Post)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Idempotency-Key", out var idempotencyKey) || string.IsNullOrEmpty(idempotencyKey))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Missing required X-Idempotency-Key header.");
            return;
        }

        string lockKey = $"idempotency:{idempotencyKey}";

        bool acquiredLock = await _redis.StringSetAsync(lockKey, "IN_PROGRESS", TimeSpan.FromMinutes(2), When.NotExists);

        if (!acquiredLock)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                Error = "Duplicate Request Detected",
                Message = "A transaction with this X-Idempotency-Key is currently processing or has already completed."
            });
            return;
        }

        await _next(context);
    }
}
