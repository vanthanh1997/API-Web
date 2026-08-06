# =============================================================================
# Dockerfile cho Worker (tiến trình CHẠY job Hangfire).
# Build từ THƯ MỤC GỐC của repo:
#     docker build -f Worker/Dockerfile -t webshop-worker .
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore trước bằng file .csproj để Docker cache lại tầng NuGet.
COPY Domain/Domain.csproj                   Domain/
COPY Application/Application.csproj         Application/
COPY Infrastructure/Infrastructure.csproj   Infrastructure/
COPY Worker/Worker.csproj                   Worker/
RUN dotnet restore Worker/Worker.csproj

COPY Domain/         Domain/
COPY Application/    Application/
COPY Infrastructure/ Infrastructure/
COPY Worker/         Worker/

RUN dotnet publish Worker/Worker.csproj \
    -c Release -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

# Worker chỉ có một endpoint /health (khai trong Worker/Program.cs).
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Worker.dll"]
