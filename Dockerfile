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

# Uploaded photos live here; mount a volume to keep them across deployments.
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID /app/wwwroot/uploads
VOLUME /app/wwwroot/uploads

ENV ASPNETCORE_HTTP_PORTS=8080 \
    TZ=Europe/Stockholm
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Rentilo.dll"]
