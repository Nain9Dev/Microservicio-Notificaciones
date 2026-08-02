# 🚀 NainDev Cloud Notification Microservice (.NET 10)

[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen?style=for-the-badge)](https://www.naindev.com)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2B%20DDD-00C7B7?style=for-the-badge)](https://www.naindev.com)
[![Messaging](https://img.shields.io/badge/Broker-RabbitMQ%20%7C%20MassTransit-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white)](https://masstransit-project.com/)

Sistema desacoplado y altamente escalable para la gestión y despacho asíncrono de notificaciones, diseñado bajo rigurosos principios de **Clean Architecture**, **Domain-Driven Design (DDD)** y **SOLID**. Integrado activamente como motor de eventos y contacto en tiempo real para el portafolio web **[www.naindev.com](https://www.naindev.com)**.

---

## 🔥 Novedades de la Demo Funcional
* **Arquitectura Desacoplada Gateway + Worker:** Separación completa entre la recepción de eventos vía HTTP REST (API Gateway) y el procesamiento intensivo en segundo plano (Worker Service).
* **Motor de Plantillas HTML Premium:** Generador responsivo de correos estilizados en modo oscuro corporativo (*NainDev Dark Aesthetics*), adaptado automáticamente según el tipo de evento (Formulario de Contacto, Bienvenida, Alerta de Sistema).
* **Resiliencia & Tolerancia a Fallos:** Políticas de reintento exponencial (*Exponential Backoff*) gestionadas vía **MassTransit & RabbitMQ**, con soporte *Failsafe Demo Logger* para ejecutar en local sin necesidad de credenciales SMTP de pago.
* **CORS & OpenAPI Lista para Producción:** Interfaz interactiva Swagger/OpenAPI integrada y políticas de seguridad configuradas para conectar al instante con el front-end de `naindev.com`.

---

## 🛠️ Stack Tecnológico
* **Core & Runtime:** .NET 10.0 (Web API Gateway + Background Worker Service).
* **Messaging Broker:** MassTransit (v8.3.4) & RabbitMQ (Alpine Management).
* **Email & Delivery Engine:** MailKit & MimeKit, compatible con servidores SMTP, Mailtrap, Resend y Webhooks directos (Discord/Telegram).
* **DevOps & Containers:** Docker Multi-stage builds & Docker Compose V2 cluster orquestación.

---

## 🏛️ Estructura del Ecosistema (Clean Architecture)
```text
Microservicio-Notificaciones/
├── src/
│   ├── Notificaciones.Domain/         # Core inmutable, eventos y enumeraciones (Sin dependencias)
│   ├── Notificaciones.Application/    # Interfaces de despacho, motor de plantillas HTML y consumidor
│   ├── Notificaciones.Infrastructure/ # Adaptadores externos (SMTP MailKit, RabbitMQ Settings, Webhooks)
│   ├── Notificaciones.Worker/         # Host de procesamiento en segundo plano (Consumidor silencioso)
│   └── Notificaciones.Api/            # Gateway HTTP REST con CORS y documentación interactiva Swagger
├── docker-compose.yml                 # Orquestación completa del clúster (RabbitMQ + API + Worker)
└── Dockerfile                         # Compilación multi-etapa .NET 10 optimizada
```

---

## ⚡ Guía Rápida de Despliegue (1-Click Demo)

Para levantar el ecosistema completo en tu máquina local o servidor con **Docker**, simplemente ejecuta desde la raíz del proyecto:

```bash
docker-compose up -d --build
```

Esto iniciará coordinada y simultáneamente 3 contenedores:
1. **RabbitMQ Bus & Management Panel:** Accesible en [http://localhost:15672](http://localhost:15672) (*guest / guest*).
2. **NainDev API Gateway & Swagger UI:** Accesible en **[http://localhost:5000/swagger](http://localhost:5000/swagger)**.
3. **Background Worker Engine:** Consumiendo de la cola de forma silenciosa y procesando plantillas.

### 🧪 Cómo probar la Demo de forma Interactiva:
1. Entra en tu navegador a `http://localhost:5000/swagger`.
2. Explora el endpoint `POST /api/v1/Notifications/demo` o `POST /api/v1/Notifications/contact`.
3. Ejecuta una petición y obtendrás una respuesta inmediata `202 Accepted` con un `TrackingId` único, confirmando la delegación al broker.
4. Para inspeccionar en vivo cómo el Worker consume el mensaje y genera el correo en menos de 500ms, visualiza sus logs con:
   ```bash
   docker logs -f notificaciones-worker
   ```

---

## 🌐 Integración con NainDev.com
El Gateway API expone el endpoint dedicado `/api/v1/notifications/contact`, optimizado para conectarse mediante `fetch` o `axios` desde el formulario de contacto en **https://www.naindev.com**, garantizando un rendimiento óptimo (cero bloqueos por envíos de correo síncronos) y máxima protección contra fallos.

&copy; 2026 NainDev &bull; Desarrollado con pasión, Clean Architecture y .NET 10.
