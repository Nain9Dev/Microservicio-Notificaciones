# Cloud Notification Microservice (.NET 8)

[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)](https://github.com/NainDev/Microservicio-Notificaciones)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Architecture](https://img.shields.io/badge/Architecture-Clean--Arch-orange)](https://dotnet.microsoft.com/en-us/apps/aspnet/architecture)

Sistema desacoplado para el envío de notificaciones diseñado bajo los principios de **Clean Architecture** y **Domain-Driven Design (DDD)**. Este microservicio utiliza **RabbitMQ** como message broker para garantizar la escalabilidad y el procesamiento asíncrono de tareas críticas en segundo plano.

## Stack Tecnológico
* **Runtime:** .NET 8 (Worker Service).
* **Messaging:** MassTransit (v8.3.4) & RabbitMQ (CloudAMQP).
* **Architecture:** Clean Architecture, DDD, SOLID Principles.
* **Infrastructure:** Docker (Multi-stage build).
* **QA:** Logging estructurado y manejo resiliente de excepciones.

## Arquitectura del Proyecto
Para asegurar el desacoplamiento total y la mantenibilidad, el proyecto se divide en las siguientes capas:

* **Domain:** Contiene los contratos de eventos de dominio (`NotificationEvent`), definidos como registros inmutables.
* **Application:** Implementa la lógica de consumo (`NotificationConsumer`) que procesa los mensajes del bus.
* **Infrastructure:** Gestiona la configuración del bus de servicios y la conexión con el broker externo.
* **Worker (Host):** Punto de entrada del microservicio, encargado de la Inyección de Dependencias y el ciclo de vida del proceso.

## Estructura de Carpetas
```text
src/
├── Notificaciones.Domain/         # Core inmutable y eventos
├── Notificaciones.Application/    # Consumidores y lógica de negocio
├── Notificaciones.Infrastructure/ # Implementación de mensajería
└── Notificaciones.Worker/         # Host del servicio y configuración
