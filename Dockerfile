# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY ["src/Notificaciones.Domain/Notificaciones.Domain.csproj", "src/Notificaciones.Domain/"]
COPY ["src/Notificaciones.Application/Notificaciones.Application.csproj", "src/Notificaciones.Application/"]
COPY ["src/Notificaciones.Infrastructure/Notificaciones.Infrastructure.csproj", "src/Notificaciones.Infrastructure/"]
COPY ["src/Notificaciones.Worker/Notificaciones.Worker.csproj", "src/Notificaciones.Worker/"]
RUN dotnet restore "src/Notificaciones.Worker/Notificaciones.Worker.csproj"

COPY . .
RUN dotnet publish "src/Notificaciones.Worker/Notificaciones.Worker.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "Notificaciones.Worker.dll"]