# ──────────────────────────────────────────────────────────────
#  ATO Copilot — Multi-stage Docker Build
# ──────────────────────────────────────────────────────────────

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY Ato.Copilot.sln ./
COPY src/Ato.Copilot.Core/Ato.Copilot.Core.csproj src/Ato.Copilot.Core/
COPY src/Ato.Copilot.State/Ato.Copilot.State.csproj src/Ato.Copilot.State/
COPY src/Ato.Copilot.Agents/Ato.Copilot.Agents.csproj src/Ato.Copilot.Agents/
COPY src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj src/Ato.Copilot.Mcp/

# Restore
RUN dotnet restore

# Copy source
COPY src/ src/

# Build
RUN dotnet publish src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r atocopilot && useradd -r -g atocopilot atocopilot

# Create data directories
RUN mkdir -p /data /app/logs && chown -R atocopilot:atocopilot /data /app/logs

# Copy published app
COPY --from=build /app/publish .

# Switch to non-root user
USER atocopilot

EXPOSE 3001

ENTRYPOINT ["dotnet", "Ato.Copilot.Mcp.dll", "--http"]
