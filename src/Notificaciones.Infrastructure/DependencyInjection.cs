using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notificaciones.Application.Consumers;
using Notificaciones.Application.Services;
using Notificaciones.Infrastructure.Services;
using Notificaciones.Infrastructure.Settings;

namespace Notificaciones.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all messaging broker integrations and resilient retry policies.
    /// </summary>
    public static IServiceCollection AddMessagingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<NotificationConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                if (!string.IsNullOrWhiteSpace(rabbitSettings.ConnectionString))
                {
                    cfg.Host(new Uri(rabbitSettings.ConnectionString));
                }
                else
                {
                    cfg.Host(rabbitSettings.Host, rabbitSettings.VirtualHost, h =>
                    {
                        h.Username(rabbitSettings.Username);
                        h.Password(rabbitSettings.Password);
                    });
                }

                // Configure resiliency and exponential backoff retry policy
                cfg.UseMessageRetry(r => r.Exponential(
                    rabbitSettings.RetryCount,
                    TimeSpan.FromSeconds(rabbitSettings.RetryIntervalSeconds),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(5)));

                cfg.ReceiveEndpoint(rabbitSettings.QueueName, e =>
                {
                    e.ConfigureConsumer<NotificationConsumer>(context);
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Registers notification delivery providers, template engines, and HTTP client factory.
    /// </summary>
    public static IServiceCollection AddNotificationProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationSettings>(configuration.GetSection(NotificationSettings.SectionName));
        services.Configure<WebhookSettings>(configuration.GetSection(WebhookSettings.SectionName));

        services.AddHttpClient("WebhookClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<EmailTemplateEngine>();
        services.AddScoped<INotificationSender, SmtpEmailSender>();

        return services;
    }
}
