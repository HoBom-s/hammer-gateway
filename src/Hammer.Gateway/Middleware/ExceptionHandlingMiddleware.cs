using System.Net;
using System.Text.Json;
using Hammer.Gateway.Kafka;
using Hammer.Gateway.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hammer.Gateway.Middleware;

internal sealed class ExceptionHandlingMiddleware
{
    private const string Topic = "service-error-log";
    private const string Source = "hammer-gateway";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IKafkaProducer kafkaProducer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(kafkaProducer);

        try
        {
            await _next(context);
        }
#pragma warning disable CA1031 // Global exception handler must catch all
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            var traceId = context.Items["TraceId"]?.ToString() ?? context.TraceIdentifier;

            ServiceErrorEvent errorEvent = new(
                TraceId: traceId,
                Source: Source,
                Level: "Error",
                ExceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                Message: ex.Message,
                StackTrace: ex.StackTrace,
                RequestPath: context.Request.Path,
                RequestMethod: context.Request.Method,
                Timestamp: DateTimeOffset.UtcNow);

            var json = JsonSerializer.Serialize(errorEvent, _jsonOptions);
            _ = kafkaProducer.ProduceAsync(Topic, traceId, json);

            ProblemDetails problem = new()
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred.",
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = traceId },
            };

            context.Response.StatusCode = problem.Status.Value;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
