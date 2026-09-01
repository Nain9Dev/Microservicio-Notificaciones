# Requisitos

- `RF-001` El sistema debe procesar solicitudes HTTP en < 50ms devolviendo `202 Accepted`.
- `RF-002` El Worker debe consumir de RabbitMQ y despachar por SMTP o Mailtrap.
- `RF-003` Los fallos de red deben desencadenar reintentos exponenciales.
- `RF-004` El sistema debe permitir la ejecución en modo "simulación" si no hay credenciales disponibles.
