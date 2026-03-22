using FluentAssertions;
using Hammer.Gateway.Middleware;
using Microsoft.AspNetCore.Http;

namespace Hammer.Gateway.Tests.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsAllSecurityHeaders()
    {
        var context = new DefaultHttpContext();

        static Task Next(HttpContext ctx) => Task.CompletedTask;
        var middleware = new SecurityHeadersMiddleware(Next);

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        context.Response.Headers["Permissions-Policy"].ToString()
            .Should().Be("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;

        Task Next(HttpContext ctx)
        {
            nextCalled = true;
            return Task.CompletedTask;
        }

        var middleware = new SecurityHeadersMiddleware(Next);

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
