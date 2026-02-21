# ═════════════════════════════════════════════════════════════════════════════=
# Stage 1 — Build Angular SPA
# ═════════════════════════════════════════════════════════════════════════════=
FROM node:22-alpine AS angular-build

WORKDIR /app/brockui

# Copy package files first for caching
COPY brockui/package*.json ./
RUN npm ci --silent

# Copy Angular source and build
COPY brockui/ ./
RUN npm run build -- --configuration production
# Output: /app/brockui/dist/brockui/browser/

# ═════════════════════════════════════════════════════════════════════════════=
# Stage 2 — Build .NET application
# Repo layout (per your screenshot):
#   /BrockLee/BrockLee.slnx
#   /BrockLee/BrockLee/BrockLee.csproj
#   /BrockLee/BrockLee.Tests/...
# ═════════════════════════════════════════════════════════════════════════════=
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build

WORKDIR /src

# Copy solution + project first for restore layer caching
COPY BrockLee/BrockLee.slnx ./BrockLee/
COPY BrockLee/BrockLee/BrockLee.csproj ./BrockLee/BrockLee/

# Restore via csproj (works even without .sln restore)
RUN dotnet restore ./BrockLee/BrockLee/BrockLee.csproj

# Copy remaining .NET source
COPY BrockLee/ ./BrockLee/

# Copy Angular build output to .NET wwwroot (served by ASP.NET Core)
COPY --from=angular-build /app/brockui/dist/brockui/browser/ ./BrockLee/BrockLee/wwwroot/

# Publish .NET
RUN dotnet publish ./BrockLee/BrockLee/BrockLee.csproj \
    -c Release -o /app/publish --no-restore

# ═════════════════════════════════════════════════════════════════════════════=
# Stage 3 — Python sidecar build (dependency install happens here)
# Uses python:3.12-slim where pip installs are allowed
# ═════════════════════════════════════════════════════════════════════════════=
FROM python:3.12-slim AS python-build

WORKDIR /python

# System build deps for scientific Python wheels (safe)
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc g++ \
    && rm -rf /var/lib/apt/lists/*

# Install Python deps into this stage
COPY PythonScripts/requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
 && pip install --no-cache-dir -r requirements.txt

# Copy Python source
COPY PythonScripts/ ./

# Optional Kaggle dataset directory (safe if empty / absent locally)
COPY PythonScripts/data/ ./data/

# ═════════════════════════════════════════════════════════════════════════════=
# Stage 4 — Final runtime image
# IMPORTANT: dotnet/aspnet image is PEP 668 "externally managed", so we must
# use a virtual environment for pip installs (no system-wide pip).
# ═════════════════════════════════════════════════════════════════════════════=
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install Python runtime + venv + supervisord + curl
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 python3-venv supervisor curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# ── .NET published output ────────────────────────────────────────────────────
COPY --from=dotnet-build /app/publish ./

# ── Python source ────────────────────────────────────────────────────────────
# Includes requirements.txt
COPY --from=python-build /python /python

# ── Python virtual environment + deps ────────────────────────────────────────
# PEP 668 compliant install (no system site-packages writes)
RUN python3 -m venv /opt/venv \
 && /opt/venv/bin/pip install --no-cache-dir --upgrade pip \
 && /opt/venv/bin/pip install --no-cache-dir -r /python/requirements.txt

# ── supervisord config ───────────────────────────────────────────────────────
COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Expose only the .NET port (Python stays internal)
EXPOSE 5477

HEALTHCHECK --interval=30s --timeout=10s --start-period=90s --retries=3 \
    CMD curl -f http://localhost:5477/health || exit 1

CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]