# BrockLee — Auto-Savings Retirement Planner (Full-Stack)

This repository contains a full-stack solution for the Auto-Savings Retirement challenge:

- **Angular SPA** dashboard (built and served by the .NET app in Docker)
- **.NET 10 ASP.NET Core API** (primary gateway, caching, logs, SignalR)
- **Python FastAPI compute + ML sidecar** (compute engine + 5 ML models; supports synthetic + Kaggle blend)
- **RabbitMQ** (async ML prediction workflow / scalable processing)
- **SQL Server / Azure SQL** database (reviewer-friendly local mode + optional Azure mode)

---

## Architecture (High Level)

- Angular calls `.NET API` on port `5477`
- .NET delegates compute to Python sidecar on `http://localhost:8000` *(internal to container)*
- .NET uses RabbitMQ for asynchronous ML prediction processing
- Logs are stored in SQL Server (local container) or Azure SQL (optional)
- SignalR pushes:
  - prediction completion events
  - dashboard refresh events

---

## Prerequisites

### Required
- Docker Desktop (Windows/Mac) or Docker Engine (Linux)
- Docker Compose (Docker Desktop includes it)

### Optional
- Kaggle dataset CSV for ML training enhancement:
  - `PythonScripts/data/salary_data.csv`
  - If not provided, the ML training uses **synthetic data only** (still works)

---

## Repository Setup (Important)

This repository includes a **template env file**:

- ✅ Commit: `.env.example` *(safe placeholders / defaults)*
- ❌ Do not commit: `.env` *(contains secrets)*

Create a local `.env` file in the repository root:

```bash
cp .env.example .env
```

---

## Quick Start — Reviewer / Local Mode (No Azure Required)

This mode starts **everything locally**, including a SQL Server container.

### 1) Create `.env`

```bash
cp .env.example .env
```

Ensure these keys exist in `.env` (defaults in `.env.example` are valid):

- `MSSQL_SA_PASSWORD` (strong password)
- `ConnectionStrings__DefaultConnection` (points to `mssql` service)
- `RABBITMQ_PASSWORD`

### 2) Run using the local profile

```bash
docker compose --profile local up --build
```

### 3) Open the application
- UI + API: http://localhost:5477
- Health: http://localhost:5477/health
- RabbitMQ UI: http://localhost:15672  
  - user: `brocklee`  
  - pass: value of `RABBITMQ_PASSWORD`

---

## Azure SQL Mode (Optional)

If you prefer Azure SQL:

1) Update `.env`:

```env
ConnectionStrings__DefaultConnection=Server=<server>.database.windows.net;Database=<db>;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;
RABBITMQ_PASSWORD=<your-password>
```

2) Start:

```bash
docker compose up --build
```

> Note: You do not need the `--profile local` flag for Azure SQL mode.

---

## Verify the Application

### 1) Health check

```bash
curl http://localhost:5477/health
```

Expected: `status: healthy`

### 2) Python sidecar health (inside container)

Python is not exposed publicly in Docker (by design). To check it:

```bash
docker exec -it blk-hacking-ind-ghanshyam-nair bash
curl http://localhost:8000/health
```

---

## API Endpoints (Main)

Base:
- `http://localhost:5477/blackrock/challenge/v1`

Core:
- `POST /transactions:parse`
- `POST /transactions:validator`
- `POST /transactions:filter`
- `POST /returns:nps`
- `POST /returns:index`
- `POST /returns:compute` *(main)*
- `POST /predictions` *(optional direct fallback)*
- `GET  /predictions/models`

Logs / Metrics:
- `GET /logs?count=50`
- `GET /logs/metrics`
- `GET /performance`

SignalR:
- `/hubs/dashboard`

---

## Test Automation

### Postman

A Postman collection is included:

- `test/BrockLee.postman_collection.json`

Import into Postman and set:
- `baseUrl = http://localhost:5477`

Run the collection in order.

---

## Kaggle Dataset (Optional)

Place your Kaggle dataset CSV here:

```text
PythonScripts/data/salary_data.csv
```

Expected metadata columns (typical):
- Age
- Gender
- Education Level
- Job Title
- Years of Experience
- Salary

If the file is missing, the Python ML service automatically falls back to synthetic training data.

---

## Notes for Reviewers

- **No SQLite** is used (SQL Server / Azure SQL only).
- Python is the primary compute engine (Option B).
- ML predictions are processed asynchronously:
  - queue job (RabbitMQ) → Python `/predict`
  - SignalR notifies the UI when predictions are ready
- If SignalR/queue is unavailable, the UI falls back to HTTP `POST /predictions`.

---

## Troubleshooting

### Docker command not found (Windows PowerShell)
Install Docker Desktop and reopen your terminal:
- https://www.docker.com/products/docker-desktop/

Verify:
```powershell
docker --version
docker compose version
```

### SQL Server container password errors
SQL Server requires a strong password. Use the default from `.env.example` or set your own that includes:
- uppercase + lowercase + number + symbol, length ≥ 8

Update both:
- `MSSQL_SA_PASSWORD`
- `ConnectionStrings__DefaultConnection` password segment

---