# ═════════════════════════════════════════════════════════════════════════════=
# Stage 1 — Build Angular SPA
# ══��══════════════════════════════════════════════════════════════════════════=
FROM node:22-alpine AS angular-build

WORKDIR /app/brockui

COPY brockui/package*.json ./
RUN npm ci --silent

COPY brockui/ ./
RUN npm run build -- --configuration production
# Output: /app/brockui/dist/brockui/browser/

# ═════════════════════════════════════════════════════════════════════════════=
# Stage 2 — Build .NET application
# Repo layout:
#   /BrockLee/BrockLee.slnx
#   /BrockLee/BrockLee/BrockLee.csproj
# ═════════════════════════════════════════════════════════════════════════════=
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build

WORKDIR /src

# Copy solution file first (layer cache)
COPY BrockLee/BrockLee.slnx ./BrockLee/

# Copy csproj next (layer cache)
COPY BrockLee/BrockLee/BrockLee.csproj ./BrockLee/BrockLee/

# Restore using csproj path (works even without .sln)
RUN dotnet restore ./BrockLee/BrockLee/BrockLee.csproj

# Copy the rest of the .NET source
COPY BrockLee/ ./BrockLee/

# Copy Angular build output → .NET wwwroot
COPY --from=angular-build /app/brockui/dist/brockui/browser/ ./BrockLee/BrockLee/wwwroot/

# Publish .NET
RUN dotnet publish ./BrockLee/BrockLee/BrockLee.csproj \
    -c Release -o /app/publish --no-restore

# ═════════════════════════════════════════════════════════════════════════════=
# Stage 3 — Python ML sidecar setup
# ═════════════════════════════════════════════════════════════════════════════=
FROM python:3.12-slim AS python-build

WORKDIR /python

RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc g++ \
    && rm -rf /var/lib/apt/lists/*

COPY PythonScripts/requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
 && pip install --no-cache-dir -r requirements.txt

COPY PythonScripts/ ./
# Kaggle CSV is optional; app falls back to synthetic data if missing
COPY PythonScripts/data/ ./data/

# ═════════════════════════════════════════════════════════════════════════════=
# Stage 4 — Final runtime image
# ═════════════════════════════════════════════════════════════════════════════=
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 supervisor curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# .NET published output
COPY --from=dotnet-build /app/publish ./

# Python source + packages
COPY --from=python-build /python /python
COPY --from=python-build /usr/local/lib/python3.12/dist-packages \
                         /usr/local/lib/python3.12/dist-packages
COPY --from=python-build /usr/local/bin/uvicorn /usr/local/bin/uvicorn

# supervisord
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

EXPOSE 5477

HEALTHCHECK --interval=30s --timeout=10s --start-period=90s --retries=3 \
    CMD curl -f http://localhost:5477/health || exit 1

CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]