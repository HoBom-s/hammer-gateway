using System.Diagnostics;
using System.Text.Json;
using Hammer.Gateway.Kafka;
using Hammer.Gateway.Models;

namespace Hammer.Gateway.Middleware;

internal sealed class RequestLoggingMiddleware
{
    private const string Topic = "gateway-request-log";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IKafkaProducer kafkaProducer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(kafkaProducer);

        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        var traceId = context.Items["TraceId"]?.ToString() ?? context.TraceIdentifier;
        var userId = context.User.FindFirst("sub")?.Value;

        GatewayRequestEvent requestEvent = new(
            TraceId: traceId,
            UserId: userId,
            Method: context.Request.Method,
            Path: context.Request.Path,
            StatusCode: context.Response.StatusCode,
            DurationMs: stopwatch.ElapsedMilliseconds,
            Timestamp: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(requestEvent, _jsonOptions);
        _ = kafkaProducer.ProduceAsync(Topic, traceId, json);
    }
}
