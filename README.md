# Microservicio de Notificaciones (NainDev)

![.NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet)
![MassTransit](https://img.shields.io/badge/MassTransit-8.3.4-blue)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-Message%20Broker-ff6600)

Un ecosistema de despacho asíncrono de correos diseñado con **Clean Architecture** y **Domain-Driven Design**. Separa completamente el *Gateway HTTP* del *Worker* de procesamiento para garantizar una latencia inferior a 50ms en la ingesta, incluso con caídas del proveedor SMTP (Resend/Mailtrap).

Este repositorio aplica el flujo **Spec-Driven Development (SDD)**: todo cambio nace en la documentación (`docs/` y `specs/`) antes que en el código.

---

## ⚡ 1-Click Demo (Docker)

Levanta la arquitectura distribuida (Gateway + Worker + RabbitMQ) en tu máquina sin instalar nada más que Docker:

```bash
docker-compose up -d --build
```

- 📡 **Swagger UI / API**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- 🐇 **RabbitMQ Admin**: [http://localhost:15672](http://localhost:15672) *(guest / guest)*
- 📜 **Logs en vivo**: `docker logs -f notificaciones-worker`

Haz un `POST` al endpoint `/demo` y observa cómo el Worker encola y procesa la plantilla en milisegundos de forma transparente.

---

## 🏛️ Diseño y Patrones Aplicados

El ecosistema implementa separación física y lógica extrema:

1. **Inmutabilidad y Eventos**: `Notificaciones.Domain` define enumeraciones y `NotificationLifecycleEvent` sin ninguna dependencia a librerías externas.
2. **Motor de Plantillas Resiliente**: Generación de correos Multipart (HTML + Texto) con blindaje VML para clientes difíciles como Outlook.
3. **Privacidad por Diseño**: `PrivacyMasker` asegura que ningún PII (Personal Identifiable Information) se filtre a los logs del clúster.
4. **Modo Híbrido**: Puede correr entero en 1 solo proceso (`UseInMemoryTransport`) o escalar horizontalmente con múltiples workers atacando RabbitMQ.

> **Toda la documentación arquitectónica profunda reside en la carpeta `docs/`.**
