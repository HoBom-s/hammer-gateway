using System.Diagnostics;
using FluentAssertions;
using Hammer.Gateway.Middleware;
using Microsoft.AspNetCore.Http;

namespace Hammer.Gateway.Tests.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsTraceId_FromTraceIdentifier_WhenNoActivityAsync()
    {
        var context = new DefaultHttpContext();

        static Task Next(HttpContext ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(Next);

        await middleware.InvokeAsync(context);

        context.Items["TraceId"].Should().Be(context.TraceIdentifier);
        context.Response.Headers["X-Trace-Id"].ToString().Should().Be(context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsync_SetsTraceId_FromActivity_WhenPresentAsync()
    {
        using var activity = new Activity("test");
        activity.Start();
        var expectedTraceId = activity.TraceId.ToString();

        var context = new DefaultHttpContext();

        static Task Next(HttpContext ctx) => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(Next);

        await middleware.InvokeAsync(context);

        context.Items["TraceId"].Should().Be(expectedTraceId);
        context.Response.Headers["X-Trace-Id"].ToString().Should().Be(expectedTraceId);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddlewareAsync()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;

        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new CorrelationIdMiddleware(Next);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
