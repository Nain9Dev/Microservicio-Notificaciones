# Arquitectura

El ecosistema se divide en 5 proyectos (Clean Architecture):

```text
src/
├── Notificaciones.Domain/         # Eventos inmutables y enumeraciones. Sin dependencias
├── Notificaciones.Application/    # Consumidor, motor de plantillas y contratos de envio
├── Notificaciones.Infrastructure/ # Adaptadores: SMTP MailKit, RabbitMQ, webhooks
├── Notificaciones.Worker/         # Host de procesamiento en segundo plano
└── Notificaciones.Api/            # Gateway REST + consola en vivo (wwwroot)
```

Las dependencias apuntan siempre hacia el dominio. `Domain` no conoce a nadie; `Application` define el caso de uso sin saber qué transporte ni qué proveedor lo ejecutan.
