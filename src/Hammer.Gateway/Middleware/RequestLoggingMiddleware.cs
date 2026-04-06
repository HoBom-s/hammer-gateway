using System.Diagnostics;
using System.Text.Json;
using Hammer.Gateway.Kafka;
using Hammer.Gateway.Models;
using Yarp.ReverseProxy.Model;

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

    public async Task InvokeAsync(HttpContext context, IKafkaProducer kafkaProducer, ILogger<RequestLoggingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(kafkaProducer);

        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        try
        {
            var traceId = context.Items["TraceId"]?.ToString() ?? context.TraceIdentifier;
            var userId = context.User.FindFirst("sub")?.Value;
            IReverseProxyFeature? proxyFeature = context.Features.Get<IReverseProxyFeature>();

            GatewayRequestEvent requestEvent = new(
                TraceId: traceId,
                UserId: userId,
                Method: context.Request.Method,
                Path: context.Request.Path,
                QueryString: context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                ClientIp: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null,
                RequestSize: context.Request.ContentLength,
                ResponseSize: context.Response.ContentLength,
                RouteCluster: proxyFeature?.Route?.Config?.ClusterId,
                StatusCode: context.Response.StatusCode,
                DurationMs: stopwatch.ElapsedMilliseconds,
                Timestamp: DateTimeOffset.UtcNow);

            var json = JsonSerializer.Serialize(requestEvent, _jsonOptions);
            _ = kafkaProducer.ProduceAsync(Topic, traceId, json);
        }
#pragma warning disable CA1031 // Logging failure must not break gateway
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Failed to publish request log event");
        }
    }
}
