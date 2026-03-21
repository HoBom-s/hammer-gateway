using System.Diagnostics;

namespace Hammer.Gateway.Middleware;

internal sealed class CorrelationIdMiddleware
{
    private const string TraceIdHeader = "X-Trace-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var traceId = Activity.Current?.TraceId.ToString()
            ?? context.TraceIdentifier;

        context.Items["TraceId"] = traceId;
        context.Response.Headers[TraceIdHeader] = traceId;

        await _next(context);
    }
}
