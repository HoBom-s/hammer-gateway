using System.Text.Json;
using FluentAssertions;
using Hammer.Gateway.Kafka;
using Hammer.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Hammer.Gateway.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<ExceptionHandlingMiddleware> _logger =
        Substitute.For<ILogger<ExceptionHandlingMiddleware>>();

    private readonly IKafkaProducer _kafkaProducer = Substitute.For<IKafkaProducer>();

    [Fact]
    public async Task InvokeAsync_Returns500ProblemDetails_OnExceptionAsync()
    {
        var context = new DefaultHttpContext();
        context.Items["TraceId"] = "trace-err";
        context.Request.Method = "POST";
        context.Request.Path = "/hammer-users/auth/login";
        context.Response.Body = new MemoryStream();

        static Task Next(HttpContext ctx) => throw new TimeoutException("downstream timed out");
        var middleware = new ExceptionHandlingMiddleware(Next, _logger);

        await middleware.InvokeAsync(context, _kafkaProducer);

        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Contain("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body, _deserializeOptions);

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(500);
        problem.Title.Should().Be("Internal Server Error");
        problem.Detail.Should().Be("An unexpected error occurred.");
        problem.Instance.Should().Be("/hammer-users/auth/login");
        problem.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task InvokeAsync_PublishesServiceErrorEvent_OnExceptionAsync()
    {
        var context = new DefaultHttpContext();
        context.Items["TraceId"] = "trace-err-2";
        context.Request.Method = "GET";
        context.Request.Path = "/hammer-users/api/profile";
        context.Response.Body = new MemoryStream();

        static Task Next(HttpContext ctx) => throw new InvalidOperationException("something broke");
        var middleware = new ExceptionHandlingMiddleware(Next, _logger);

        await middleware.InvokeAsync(context, _kafkaProducer);

        await _kafkaProducer.Received(1)
            .ProduceAsync("service-error-log", "trace-err-2", Arg.Any<string>());

        var json = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[2]!;
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("traceId").GetString().Should().Be("trace-err-2");
        root.GetProperty("source").GetString().Should().Be("hammer-gateway");
        root.GetProperty("level").GetString().Should().Be("Error");
        root.GetProperty("exceptionType").GetString().Should().Be("System.InvalidOperationException");
        root.GetProperty("message").GetString().Should().Be("something broke");
        root.GetProperty("stackTrace").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("requestPath").GetString().Should().Be("/hammer-users/api/profile");
        root.GetProperty("requestMethod").GetString().Should().Be("GET");
        root.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_PassesThrough_WhenNoExceptionAsync()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/health";

        var nextCalled = false;

        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new ExceptionHandlingMiddleware(Next, _logger);

        await middleware.InvokeAsync(context, _kafkaProducer);

        nextCalled.Should().BeTrue();
        await _kafkaProducer.DidNotReceive()
            .ProduceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task InvokeAsync_FallsBackToTraceIdentifier_WhenTraceIdNotInItemsAsync()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();

        static Task Next(HttpContext ctx) => throw new InvalidOperationException("fail");
        var middleware = new ExceptionHandlingMiddleware(Next, _logger);

        await middleware.InvokeAsync(context, _kafkaProducer);

        var key = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[1]!;
        key.Should().Be(context.TraceIdentifier);
    }
}
