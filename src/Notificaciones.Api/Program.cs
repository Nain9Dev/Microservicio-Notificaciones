using MassTransit;
using Microsoft.OpenApi.Models;
using Notificaciones.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "NainDev Notification Cloud API", 
        Version = "v1",
        Description = "REST Gateway for decoupled, asynchronous notification processing with RabbitMQ & .NET 10. Integrated with naindev.com portfolio."
    });
});

// Configure CORS for naindev.com & local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("NainDevCorsPolicy", policy =>
    {
        policy.WithOrigins("https://www.naindev.com", "http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configure MassTransit exclusively for Publishing (Producer mode)
var rabbitSettings = builder.Configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitSettings.Host, rabbitSettings.VirtualHost, h =>
        {
            h.Username(rabbitSettings.Username);
            h.Password(rabbitSettings.Password);
        });
    });
});

var app = builder.Build();

// Configure HTTP request pipeline & interactive docs
if (app.Environment.IsDevelopment() || true) // Enable Swagger in demo environments by default
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NainDev Notification API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("NainDevCorsPolicy");

app.UseAuthorization();
app.MapControllers();

app.Run();
