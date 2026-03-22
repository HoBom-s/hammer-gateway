namespace Hammer.Gateway.Models;

internal sealed record ServiceErrorEvent(
    string TraceId,
    string Source,
    string Level,
    string ExceptionType,
    string Message,
    string? StackTrace,
    string RequestPath,
    string RequestMethod,
    DateTimeOffset Timestamp);
