# ─── Stage 1: Build React Frontend ───────────────────────────────
FROM node:20.18.1-alpine AS frontend-build

WORKDIR /app/ClientApp

# Copy package files first for layer caching
COPY src/Ato.Copilot.Chat/ClientApp/package*.json ./
RUN npm ci --silent

# Copy frontend source and build
COPY src/Ato.Copilot.Chat/ClientApp/ ./
RUN npm run build

# ─── Stage 2: Build .NET Backend ─────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-build

WORKDIR /src

# Copy solution and project files for restore caching
COPY Ato.Copilot.sln ./
COPY src/Ato.Copilot.Chat/Ato.Copilot.Chat.csproj src/Ato.Copilot.Chat/
COPY src/Ato.Copilot.Core/Ato.Copilot.Core.csproj src/Ato.Copilot.Core/
COPY src/Ato.Copilot.Agents/Ato.Copilot.Agents.csproj src/Ato.Copilot.Agents/
COPY src/Ato.Copilot.State/Ato.Copilot.State.csproj src/Ato.Copilot.State/
COPY src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj src/Ato.Copilot.Mcp/

# Restore only the Chat project and its dependencies
RUN dotnet restore src/Ato.Copilot.Chat/Ato.Copilot.Chat.csproj

# Copy all source code and publish
COPY src/ src/
RUN dotnet publish src/Ato.Copilot.Chat/Ato.Copilot.Chat.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Stage 3: Runtime ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Create non-root user per FR-040
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

WORKDIR /app

# Copy published .NET app
COPY --from=backend-build /app/publish .

# Copy built React app to wwwroot
COPY --from=frontend-build /app/ClientApp/build ./wwwroot

# Create uploads directory with proper permissions
RUN mkdir -p /app/uploads && chown -R appuser:appgroup /app/uploads

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Expose port
EXPOSE 8080

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Ato.Copilot.Chat.dll"]
