namespace Hammer.Gateway.Models;

internal sealed record GatewayRequestEvent(
    string TraceId,
    string? UserId,
    string Method,
    string Path,
    int StatusCode,
    long DurationMs,
    DateTimeOffset Timestamp);
