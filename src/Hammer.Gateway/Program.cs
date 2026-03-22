using System.Text;
using System.Threading.RateLimiting;
using Hammer.Gateway.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RedisRateLimiting;
using Serilog;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(transform =>
        {
            if (transform.HttpContext.Items["TraceId"] is string traceId)
            {
                transform.ProxyRequest.Headers.Remove("X-Trace-Id");
                transform.ProxyRequest.Headers.Add("X-Trace-Id", traceId);
            }

            var userId = transform.HttpContext.User.FindFirst("sub")?.Value;
            if (userId is not null)
            {
                transform.ProxyRequest.Headers.Remove("X-User-Id");
                transform.ProxyRequest.Headers.Add("X-User-Id", userId);
            }

            return ValueTask.CompletedTask;
        });
    });

// JWT
IConfigurationSection jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        };
    });

builder.Services.AddAuthorization();

// Redis
var redisConnectionString = builder.Configuration["Redis:ConnectionString"]!;
IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
builder.Services.AddSingleton(redis);

// Rate Limiting
var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.Get(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: key => new RedisFixedWindowRateLimiter<string>(key, new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
            })));
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddRedis(redisConnectionString, name: "redis");

WebApplication app = builder.Build();

// Middleware pipeline (order matters)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapReverseProxy();

await app.RunAsync();
