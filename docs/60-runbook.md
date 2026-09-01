# Runbook

## Arranque
### Opción A — Un solo proceso (In-Memory)
```bash
dotnet run --project src/Notificaciones.Api
```
El perfil `Development` activa `RabbitMq:UseInMemoryTransport`, que sustituye RabbitMQ por el transporte en memoria y aloja el consumidor en el propio gateway.

### Opción B — Clúster completo con Docker
```bash
docker-compose up -d --build
```
Levanta tres contenedores: RabbitMQ, API Gateway y Worker.

Para ver logs del worker:
```bash
docker logs -f notificaciones-worker
```
