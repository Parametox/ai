# syntax=docker/dockerfile:1

# =============================================================================
# Dockerfile dla KanbanLite.Web (.NET 9 Blazor Server)
# Multi-stage build: restore → build → publish → runtime
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Base - obraz SDK do budowania
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Kopiuj pliki projektów i przywróć zależności (cache layer)
COPY ["src/Web/KanbanLite.Web.csproj", "src/Web/"]
COPY ["src/Application/KanbanLite.Application.csproj", "src/Application/"]
COPY ["src/DataAccess/DataAccess.csproj", "src/DataAccess/"]
COPY ["NuGet.config", "."]

RUN dotnet restore "src/Web/KanbanLite.Web.csproj"

# Kopiuj pozostałe pliki źródłowe
COPY src/ src/

# -----------------------------------------------------------------------------
# Stage 2: Publish - budowanie i publikacja aplikacji
# -----------------------------------------------------------------------------
FROM build AS publish
WORKDIR /src/src/Web

# Buduj w trybie Release i publikuj do folderu /app/publish
RUN dotnet publish "KanbanLite.Web.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 3: Runtime - finalny obraz produkcyjny
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Ustaw zmienne środowiskowe
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Utwórz użytkownika non-root dla bezpieczeństwa
RUN adduser --disabled-password --gecos "" --uid 1000 appuser

# Kopiuj opublikowaną aplikację
COPY --from=publish /app/publish .

# Zmień właściciela plików na appuser
RUN chown -R appuser:appuser /app

# Przełącz na użytkownika non-root
USER appuser

# Eksponuj port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Uruchom aplikację
ENTRYPOINT ["dotnet", "KanbanLite.Web.dll"]
