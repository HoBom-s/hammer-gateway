using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Hammer.Gateway.Kafka;
using Hammer.Gateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Hammer.Gateway.Tests.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    private readonly IKafkaProducer _kafkaProducer = Substitute.For<IKafkaProducer>();
    private readonly ILogger<RequestLoggingMiddleware> _logger = Substitute.For<ILogger<RequestLoggingMiddleware>>();

    [Fact]
    public async Task InvokeAsync_PublishesEvent_WithAllFieldsPopulatedAsync()
    {
        var context = new DefaultHttpContext();
        context.Items["TraceId"] = "trace-abc";
        context.Request.Method = "POST";
        context.Request.Path = "/hammer-users/auth/login";
        context.Request.QueryString = new QueryString("?page=1&size=10");
        context.Request.ContentLength = 256;
        context.Request.Headers.UserAgent = "Mozilla/5.0 TestAgent";
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-42")], "test"));

        static Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength = 1024;
            return Task.CompletedTask;
        }

        var middleware = new RequestLoggingMiddleware(Next);

        await middleware.InvokeAsync(context, _kafkaProducer, _logger);

        await _kafkaProducer.Received(1)
            .ProduceAsync("gateway-request-log", "trace-abc", Arg.Any<string>());

        var json = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[2]!;
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("traceId").GetString().Should().Be("trace-abc");
        root.GetProperty("userId").GetString().Should().Be("user-42");
        root.GetProperty("method").GetString().Should().Be("POST");
        root.GetProperty("path").GetString().Should().Be("/hammer-users/auth/login");
        root.GetProperty("queryString").GetString().Should().Be("?page=1&size=10");
        root.GetProperty("clientIp").GetString().Should().Be("10.0.0.1");
        root.GetProperty("userAgent").GetString().Should().Be("Mozilla/5.0 TestAgent");
        root.GetProperty("requestSize").GetInt64().Should().Be(256);
        root.GetProperty("responseSize").GetInt64().Should().Be(1024);
        root.GetProperty("statusCode").GetInt32().Should().Be(200);
        root.GetProperty("durationMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("timestamp").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SetsNullForOptionalFields_WhenAbsentAsync()
    {
        var context = new DefaultHttpContext();
        context.Items["TraceId"] = "trace-def";
        context.Request.Method = "GET";
        context.Request.Path = "/health";

        static Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        var middleware = new RequestLoggingMiddleware(Next);

        await middleware.InvokeAsync(context, _kafkaProducer, _logger);

        var json = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[2]!;
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("userId").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("queryString").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("userAgent").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("requestSize").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("responseSize").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("routeCluster").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task InvokeAsync_FallsBackToTraceIdentifier_WhenTraceIdNotInItemsAsync()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";

        static Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        var middleware = new RequestLoggingMiddleware(Next);

        await middleware.InvokeAsync(context, _kafkaProducer, _logger);

        var key = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[1]!;
        key.Should().Be(context.TraceIdentifier);

        var json = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[2]!;
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("traceId").GetString().Should().Be(context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsync_Records429_WithClientIpAsync()
    {
        var context = new DefaultHttpContext();
        context.Items["TraceId"] = "trace-429";
        context.Request.Method = "GET";
        context.Request.Path = "/hammer-users/api/resource";
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.50");

        static Task Next(HttpContext ctx)
        {
            ctx.Response.StatusCode = 429;
            return Task.CompletedTask;
        }

        var middleware = new RequestLoggingMiddleware(Next);

        await middleware.InvokeAsync(context, _kafkaProducer, _logger);

        var json = (string)_kafkaProducer.ReceivedCalls().Single().GetArguments()[2]!;
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(429);
        root.GetProperty("clientIp").GetString().Should().Be("203.0.113.50");
    }
}
