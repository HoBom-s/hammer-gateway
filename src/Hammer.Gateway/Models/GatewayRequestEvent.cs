namespace Hammer.Gateway.Models;

internal sealed record GatewayRequestEvent(
    string TraceId,
    string? UserId,
    string Method,
    string Path,
    string? QueryString,
    string? ClientIp,
    string? UserAgent,
    long? RequestSize,
    long? ResponseSize,
    string? RouteCluster,
    int StatusCode,
    long DurationMs,
    DateTimeOffset Timestamp);
