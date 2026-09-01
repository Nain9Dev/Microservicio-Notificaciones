# Charter

## Misión
Microservicio de Notificaciones enfocado en encolamiento rápido y procesamiento asíncrono para abstraer el I/O del servidor principal.

## Metas
- Sobrevivir a caídas de los proveedores SMTP sin perder correos.
- Evitar bloquear las peticiones HTTP del gateway con envíos SMTP síncronos.
- Ofrecer un pipeline limpio, fácil de desplegar para demos de portafolio.
