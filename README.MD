# BrockLee — Auto-Savings Retirement Planner (Full-Stack)

This repository contains a full-stack solution for the Auto-Savings Retirement challenge:

- **Angular SPA** dashboard (built and served by the .NET app in Docker)
- **.NET 10 ASP.NET Core API** (primary gateway, caching, logs, SignalR)
- **Python FastAPI compute + ML sidecar** (primary compute engine + 5 ML models)
- **RabbitMQ** (async ML prediction workflow / scalable processing)
- **Azure SQL** database (used in all environments)

---

## Architecture (High Level)

- Angular calls `.NET API` on port `5477`
- .NET delegates compute to Python sidecar on `http://localhost:8000` (internal)
- .NET uses RabbitMQ for asynchronous ML prediction processing
- Logs are stored in Azure SQL
- SignalR pushes:
  - prediction completion events
  - dashboard refresh events

---

## Prerequisites

### Required
- Docker + Docker Compose
- An **Azure SQL** database connection string

### Optional
- Kaggle dataset CSV for ML training enhancement:
  - `PythonScripts/data/salary_data.csv`
  - If not provided, the ML training uses **synthetic data only** (still works).

---

## Configuration

Create a file named `.env` in the repository root (do **not** commit it).

Example:

```env
AZURE_SQL_CONNECTION_STRING=Server=YOUR_SERVER.database.windows.net;Database=brocklee;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;

RABBITMQ_PASSWORD=change-me-strong-password
```

---

## Run (Docker)

```bash
docker compose up --build
```

Services:
- App (Angular + .NET API): http://localhost:5477
- Health: http://localhost:5477/health
- RabbitMQ UI: http://localhost:15672
  - user: `brocklee`
  - pass: value of `RABBITMQ_PASSWORD`

---

## Verify the application

### 1) Health check

```bash
curl http://localhost:5477/health
```

Expected: `status: healthy`

### 2) Python sidecar verification (inside container)

Python is not exposed publicly in Docker (by design).  
To test it, exec into the container:

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
- `POST /predictions` *(direct fallback / optional)*
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
A Postman collection is included here:

- `test/BrockLee.postman_collection.json`

Import into Postman, set `baseUrl` to:

- `http://localhost:5477`

Run the collection in order.

---

## Kaggle Dataset (Optional)

Place your Kaggle dataset CSV here:

```
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

- Azure SQL is used in all environments (development + production) — no SQLite.
- Python is the **primary compute engine** (Option B).
- ML predictions run via:
  - RabbitMQ queue consumer → Python `/predict`
  - SignalR notifies the UI when predictions are ready.
- If SignalR/queue is unavailable, UI falls back to HTTP `POST /predictions`.

---