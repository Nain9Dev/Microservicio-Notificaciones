using System.Net;
using System.Threading.RateLimiting;
using MassTransit;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Notificaciones.Api.Consumers;
using Notificaciones.Api.Serialization;
using Notificaciones.Api.Settings;
using Notificaciones.Api.Telemetry;
using Notificaciones.Application.Consumers;
using Notificaciones.Infrastructure;
using Notificaciones.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

var rabbitSettings = builder.Configuration
    .GetSection(RabbitMqSettings.SectionName)
    .Get<RabbitMqSettings>() ?? new RabbitMqSettings();

var instanceId = Guid.NewGuid().ToString("N")[..8];
var runtimeInfo = new GatewayRuntimeInfo(
    TransportMode: rabbitSettings.UseInMemoryTransport ? "in-memory" : "rabbitmq",
    RunsWorkerInProcess: rabbitSettings.UseInMemoryTransport,
    InstanceId: instanceId,
    StartedAt: DateTimeOffset.UtcNow);

// ---------------------------------------------------------------------------
// Presentation layer
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = ApiJson.Options.PropertyNamingPolicy;

        foreach (var converter in ApiJson.Options.Converters)
        {
            options.JsonSerializerOptions.Converters.Add(converter);
        }
    });

// Minimal API and manual serialization paths must agree with the MVC contract.
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = ApiJson.Options.PropertyNamingPolicy;

    foreach (var converter in ApiJson.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NainDev Notification Cloud API",
        Version = "v1",
        Description = "REST gateway for decoupled, asynchronous notification processing with RabbitMQ and .NET 10. " +
                      "Ships a live operations console at the site root."
    });
});

// ---------------------------------------------------------------------------
// Cross-cutting configuration
// ---------------------------------------------------------------------------
builder.Services.Configure<DemoPolicySettings>(builder.Configuration.GetSection(DemoPolicySettings.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(runtimeInfo);
builder.Services.AddSingleton<INotificationTelemetryStore, InMemoryNotificationTelemetryStore>();
builder.Services.AddSingleton<NotificationStreamBroadcaster>();
builder.Services.AddSingleton<PipelineTelemetryIngress>();

// Registers the template engine and the delivery providers. The engine is required by the preview
// endpoint even when this process does not host the worker consumer.
builder.Services.AddNotificationProviders(builder.Configuration);

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("NainDevCorsPolicy", policy =>
    {
        policy.WithOrigins("https://www.naindev.com", "https://naindev.com", "http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ---------------------------------------------------------------------------
// Rate limiting: the gateway is publicly reachable, every write endpoint is bounded per client IP.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.ContactForm, context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolveClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.AddPolicy(RateLimitPolicies.DemoDispatch, context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolveClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.AddPolicy(RateLimitPolicies.Preview, context =>
        RateLimitPartition.GetFixedWindowLimiter(ResolveClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        await context.HttpContext.Response.WriteAsync(
            """{"status":"Rate limit exceeded","message":"Has superado el límite de peticiones. Espera un minuto e inténtalo de nuevo."}""",
            cancellationToken);
    };
});

// ---------------------------------------------------------------------------
// Messaging: the gateway always publishes and always consumes pipeline telemetry.
// In single-process demo mode it additionally hosts the notification consumer, so the whole
// architecture can be demonstrated without RabbitMQ.
// ---------------------------------------------------------------------------
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<NotificationTelemetryConsumer>();

    if (rabbitSettings.UseInMemoryTransport)
    {
        x.AddConsumer<NotificationConsumer>();
        x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitSettings.Host, rabbitSettings.VirtualHost, h =>
            {
                h.Username(rabbitSettings.Username);
                h.Password(rabbitSettings.Password);
            });

            // Non-durable, auto-deleted queue: every gateway instance receives its own copy of the
            // telemetry stream and never competes with the workers for notification messages.
            cfg.ReceiveEndpoint($"naindev-telemetry-{instanceId}", e =>
            {
                e.Durable = false;
                e.AutoDelete = true;
                e.ConfigureConsumer<NotificationTelemetryConsumer>(context);
            });
        });
    }
});

var app = builder.Build();

// Only trust proxy headers when the deployment declares the proxies it sits behind.
var forwardedSettings = builder.Configuration
    .GetSection(ForwardedHeadersSettings.SectionName)
    .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

if (forwardedSettings.Enabled)
{
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };

    // Clearing the defaults is required: otherwise only loopback proxies are accepted.
    forwardedOptions.KnownProxies.Clear();
    forwardedOptions.KnownIPNetworks.Clear();

    foreach (var proxy in forwardedSettings.KnownProxies)
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            forwardedOptions.KnownProxies.Add(address);
        }
    }

    foreach (var network in forwardedSettings.KnownNetworks)
    {
        var parts = network.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
        {
            forwardedOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(prefix, length));
        }
    }

    app.UseForwardedHeaders(forwardedOptions);
    app.Logger.LogInformation(
        "Forwarded headers enabled | trusted proxies: {Proxies} | trusted networks: {Networks}",
        forwardedOptions.KnownProxies.Count, forwardedOptions.KnownIPNetworks.Count);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NainDev Notification API v1");
    c.RoutePrefix = "swagger";
});

// Serve the operations console from wwwroot at the site root.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("NainDevCorsPolicy");
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation(
    "NainDev notification gateway ready | transport: {Transport} | in-process worker: {InProcess} | instance: {InstanceId}",
    runtimeInfo.TransportMode, runtimeInfo.RunsWorkerInProcess, runtimeInfo.InstanceId);

app.Run();

// Partitions the rate limiter per client.
//
// It deliberately reads only the socket address: trusting X-Forwarded-For directly would let any
// caller bypass every limit just by rotating the header. When the gateway runs behind a real proxy,
// enable ForwardedHeaders:Enabled with the proxy addresses and the middleware rewrites
// RemoteIpAddress from the header it has validated.
static string ResolveClientKey(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
