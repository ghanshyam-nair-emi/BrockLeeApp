# ══════════════════════════════════════════════════════════════════════════════
# Stage 1 — Build Angular SPA
# ══════════════════════════════════════════════════════════════════════════════
FROM node:22-alpine AS angular-build

WORKDIR /app/brockui

# Copy package files first for layer caching
COPY brockui/package*.json ./
RUN npm ci --silent

# Copy Angular source and build for production
COPY brockui/ ./
RUN npm run build -- --configuration production

# Output: /app/brockui/dist/brockui/browser/

# ══════════════════════════════════════════════════════════════════════════════
# Stage 2 — Build .NET application
# ══════════════════════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build

WORKDIR /app

# Copy solution and project files first for layer caching
COPY *.sln ./
COPY BrockLee/*.csproj ./BrockLee/
RUN dotnet restore BrockLee/BrockLee.csproj

# Copy rest of .NET source
COPY BrockLee/ ./BrockLee/

# Copy Angular build output → .NET wwwroot
# This is what makes .NET serve the Angular SPA
COPY --from=angular-build /app/brockui/dist/brockui/browser/ ./BrockLee/wwwroot/

# Publish .NET app
RUN dotnet publish BrockLee/BrockLee.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ══════════════════════════════════════════════════════════════════════════════
# Stage 3 — Python ML sidecar setup
# ══════════════════════════════════════════════════════════════════════════════
FROM python:3.12-slim AS python-build

WORKDIR /python

# System deps needed by scikit-learn / numpy
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    g++ \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install — separate layer for caching
COPY PythonScripts/requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
 && pip install --no-cache-dir -r requirements.txt

# Copy Python source
COPY PythonScripts/ ./

# Copy Kaggle dataset if present — optional, gracefully skipped if absent
# Place salary_data.csv in PythonScripts/data/ before building
COPY PythonScripts/data/ ./data/

# ══════════════════════════════════════════════════════════════════════════════
# Stage 4 — Final runtime image
# ══════════════════════════════════════════════════════════════════════════════
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install Python 3.12 + supervisord in the final image
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3.12 \
    python3.12-venv \
    python3-pip \
    supervisor \
    curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# ── .NET published output ────────────────────────────────────────────────────
COPY --from=dotnet-build /app/publish ./

# ── Python source + installed packages ───────────────────────────────────────
COPY --from=python-build /python /python

# Copy installed Python packages from build stage
COPY --from=python-build /usr/local/lib/python3.12/dist-packages \
                         /usr/local/lib/python3.12/dist-packages
COPY --from=python-build /usr/local/bin/uvicorn /usr/local/bin/uvicorn

# ── supervisord config ───────────────────────────────────────────────────────
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# ── Ports ────────────────────────────────────────────────────────────────────
# 5477 = .NET API + Angular SPA (external)
# 8000 = Python FastAPI sidecar (internal only)
EXPOSE 5477

# ── Healthcheck ──────────────────────────────────────────────────────────────
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:5477/health || exit 1

# ── Entrypoint ───────────────────────────────────────────────────────────────
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]