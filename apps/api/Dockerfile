# syntax=docker/dockerfile:1
# Build from the repository root: docker build -f apps/api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY apps/api/Directory.Build.props apps/api/.editorconfig apps/api/
COPY apps/api/src/Crm.Api/Crm.Api.csproj apps/api/src/Crm.Api/
COPY apps/api/src/Crm.Application/Crm.Application.csproj apps/api/src/Crm.Application/
COPY apps/api/src/Crm.Domain/Crm.Domain.csproj apps/api/src/Crm.Domain/
COPY apps/api/src/Crm.Infrastructure/Crm.Infrastructure.csproj apps/api/src/Crm.Infrastructure/
COPY apps/api/src/Crm.Cli/Crm.Cli.csproj apps/api/src/Crm.Cli/
RUN dotnet restore apps/api/src/Crm.Api/Crm.Api.csproj \
    && dotnet restore apps/api/src/Crm.Cli/Crm.Cli.csproj

COPY apps/api/ apps/api/
RUN dotnet publish apps/api/src/Crm.Api/Crm.Api.csproj \
    --configuration Release --no-restore --output /app -p:UseAppHost=false
# Separate folder: both projects ship their own appsettings.json.
RUN dotnet publish apps/api/src/Crm.Cli/Crm.Cli.csproj \
    --configuration Release --no-restore --output /app/cli -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Alpine images ship without ICU (culture-aware string operations) and Kerberos (loaded by Npgsql).
RUN apk add --no-cache icu-libs icu-data-full krb5-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    FileStorage__UploadDirectory=/data/uploads \
    SerilogOptions__LogPath=/data/logs

# Named volumes mounted here inherit this ownership, so the non-root user can write to them.
# ASP.NET Core keeps Data Protection keys in ~/.aspnet; they must outlive the container.
RUN mkdir -p /data/uploads /data/logs /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID" /data /home/app/.aspnet

# Admin CLI: `docker compose exec api crm reset-password -e <email>`.
RUN printf '#!/bin/sh\nexec dotnet /app/cli/Crm.Cli.dll "$@"\n' > /usr/local/bin/crm \
    && chmod +x /usr/local/bin/crm

WORKDIR /app
COPY --from=build /app ./

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=3 \
    CMD wget -qO /dev/null http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Crm.Api.dll"]
