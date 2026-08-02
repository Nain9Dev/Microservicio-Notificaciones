using Notificaciones.Infrastructure;
using Notificaciones.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Register application & infrastructure services cleanly via Clean Architecture extensions
builder.Services.AddMessagingInfrastructure(builder.Configuration);
builder.Services.AddNotificationProviders(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();