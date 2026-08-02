# Stage 1: Base Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Notificaciones.Domain/Notificaciones.Domain.csproj", "src/Notificaciones.Domain/"]
COPY ["src/Notificaciones.Application/Notificaciones.Application.csproj", "src/Notificaciones.Application/"]
COPY ["src/Notificaciones.Infrastructure/Notificaciones.Infrastructure.csproj", "src/Notificaciones.Infrastructure/"]
COPY ["src/Notificaciones.Worker/Notificaciones.Worker.csproj", "src/Notificaciones.Worker/"]
COPY ["src/Notificaciones.Api/Notificaciones.Api.csproj", "src/Notificaciones.Api/"]
RUN dotnet restore "src/Notificaciones.Worker/Notificaciones.Worker.csproj"
RUN dotnet restore "src/Notificaciones.Api/Notificaciones.Api.csproj"

COPY . .
WORKDIR "/src"
RUN dotnet build "src/Notificaciones.Worker/Notificaciones.Worker.csproj" -c Release -o /app/build
RUN dotnet build "src/Notificaciones.Api/Notificaciones.Api.csproj" -c Release -o /app/build

# Stage 2: Publish Worker
FROM build AS publish-worker
RUN dotnet publish "src/Notificaciones.Worker/Notificaciones.Worker.csproj" -c Release -o /app/publish-worker /p:UseAppHost=false

# Stage 3: Publish API
FROM build AS publish-api
RUN dotnet publish "src/Notificaciones.Api/Notificaciones.Api.csproj" -c Release -o /app/publish-api /p:UseAppHost=false

# Stage 4: Worker Runtime Target
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app
COPY --from=publish-worker /app/publish-worker .
ENTRYPOINT ["dotnet", "Notificaciones.Worker.dll"]

# Stage 5: API Runtime Target
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=publish-api /app/publish-api .
ENTRYPOINT ["dotnet", "Notificaciones.Api.dll"]