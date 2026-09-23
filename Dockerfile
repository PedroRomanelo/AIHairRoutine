# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer (cached while csproj/props are unchanged).
COPY Directory.Build.props AIHairRoutine.slnx ./
COPY src/AIHairRoutine.Api/AIHairRoutine.Api.csproj src/AIHairRoutine.Api/
COPY src/AIHairRoutine.Application/AIHairRoutine.Application.csproj src/AIHairRoutine.Application/
COPY src/AIHairRoutine.Infrastructure/AIHairRoutine.Infrastructure.csproj src/AIHairRoutine.Infrastructure/
RUN dotnet restore src/AIHairRoutine.Api/AIHairRoutine.Api.csproj

# Build + publish.
COPY . .
RUN dotnet publish src/AIHairRoutine.Api/AIHairRoutine.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime (chiseled, non-root, port 8080) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Chiseled images already run as the non-root 'app' user and default to ASPNETCORE_HTTP_PORTS=8080.
# Liveness/readiness are exposed at /health and /ready for the orchestrator to probe.
ENTRYPOINT ["dotnet", "AIHairRoutine.Api.dll"]
