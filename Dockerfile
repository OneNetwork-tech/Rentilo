# syntax=docker/dockerfile:1

# ---- Build ---------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first so the layer is cached until a project file changes.
COPY src/Rentilo/Rentilo.csproj src/Rentilo/
RUN dotnet restore src/Rentilo/Rentilo.csproj

COPY src/ src/
RUN dotnet publish src/Rentilo/Rentilo.csproj -c Release -o /app --no-restore

# ---- Runtime -------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Uploaded photos and the data-protection keys (login cookies) live in volumes so they survive redeploys.
RUN mkdir -p /app/wwwroot/uploads /app/keys && chown -R $APP_UID /app/wwwroot/uploads /app/keys
VOLUME ["/app/wwwroot/uploads", "/app/keys"]

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/app/keys \
    TZ=Europe/Stockholm
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Rentilo.dll"]
