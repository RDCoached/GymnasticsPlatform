# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Restore and publish in one step
WORKDIR /src/src/GymnasticsPlatform.Api
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install curl for health checks
USER root
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Run as non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "GymnasticsPlatform.Api.dll"]
