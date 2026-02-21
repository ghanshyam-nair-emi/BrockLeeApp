# BrockLee — Auto-Savings Retirement Planner (Full-Stack)

---

> ## 🐳 IMPORTANT: Pre-Built Docker Image Available
>
> **Skip the build step entirely — pull the ready-to-run Docker image directly from Docker Hub:**
>
> 🔗 **[https://hub.docker.com/repository/docker/ghantheman/brockleeapp](https://hub.docker.com/repository/docker/ghantheman/brockleeapp)**
>
> ```bash
> docker pull ghantheman/brockleeapp
> ```
>
> This is the recommended way to get started quickly without building from source.

> ## 🎬 Submission Video
>
> **Watch the full project demo and walkthrough before getting started:**
>
> 📹 **[View Submission Video on Google Drive](https://drive.google.com/file/d/1U74BER_ZMVjaaCPL355AWxed_rgN8L3A/view?usp=sharing)**
>
> The video covers the complete feature set, architecture overview, and a live walkthrough of the application.

---

## ⚡ Build & Run (Quick Reference)

> Full setup instructions are further below. These commands are the only ones you need to get started.

**Step 1 — Copy environment file**
```bash
cp .env.example .env
```

**Step 2 — Build and start everything (local mode, no Azure required)**
```bash
docker compose --profile local up --force-recreate
```
**Step 3 — Open the app**

| Service | URL |
|---|---|
| Dashboard (UI + API) | http://localhost:5477 |
| Health check | http://localhost:5477/health |
| RabbitMQ management | http://localhost:15672 |

**Run .NET tests**
```bash
dotnet test BrockLee/BrockLee.Tests/BrockLee.Tests.csproj
```

**Run Angular dev server (standalone)**
```bash
cd brockui
npm install --legacy-peer-deps
npm run start               # http://localhost:4200
```

**Run Python sidecar standalone**
```bash
cd PythonScripts
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```

---

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

**Advanced Insights** (NEW):
- `POST /timeseries:nps` *(Year-by-year NPS projection)*
- `POST /timeseries:index` *(Year-by-year Index projection)*
- `POST /risk-profile` *(ML-based instrument recommendation)*

Logs / Metrics:
- `GET /logs?count=50`
- `GET /logs/metrics`
- `GET /performance`

SignalR:
- `/hubs/dashboard`

---

## Advanced Features: Time-Series & Risk Profiling

### 7. Time-Series Forecast (Year-by-Year Progression)

**What it does**: Projects investment growth year-by-year until retirement, showing:
- Nominal value (before inflation adjustment)
- Real/Inflation-adjusted value (purchasing power)
- Return on Investment (ROI)
- Key milestones (₹100k, ₹500k, ₹1M, ₹2M, ₹5M)

**Endpoint**: `POST /blackrock/challenge/v1/timeseries:nps` or `timeseries:index`

**Request** (JSON):
```json
{
  "principal": 145.0,
  "age": 29,
  "annual_income": 600000.0,
  "inflation": 0.055
}
```

**Response** (excerpt):
```json
{
  "principal": 145.0,
  "startAge": 29,
  "instrument": "nps",
  "timeline": [
    {
      "yearOffset": 0,
      "projectionYear": 2026,
      "age": 29,
      "balance": 145.0,
      "nominalValue": 145.0,
      "realValue": 145.0,
      "roi": 0.0,
      "growthRate": 0.0711,
      "isRetirement": false
    },
    {
      "yearOffset": 5,
      "projectionYear": 2031,
      "age": 34,
      "balance": 145.0,
      "nominalValue": 204.28,
      "realValue": 159.42,
      "roi": 14.42,
      "growthRate": 0.0711,
      "isRetirement": false
    },
    {
      "yearOffset": 31,
      "projectionYear": 2057,
      "age": 60,
      "balance": 145.0,
      "nominalValue": 1219.45,
      "realValue": 231.9,
      "roi": 86.9,
      "growthRate": 0.0711,
      "isRetirement": true
    }
  ],
  "milestones": [
    {
      "threshold": 1000000.0,
      "projectionYear": 2051,
      "yearOffset": 25,
      "age": 54,
      "realValue": 1001240.5,
      "description": "₹1000000 milestone reached"
    }
  ]
}
```

**Use Case**: 
- Show investors a visual graph of their retirement savings growth
- Highlight when they reach key financial milestones
- Demonstrate the power of compound interest year-by-year
- Compare NPS vs Index projection trajectories

---

### 3. Risk-Adjusted Recommendation Engine

**What it does**: ML-based profiling that recommends NPS or Index Fund based on:
- **Expense Volatility** (derived from transaction history)
  - 0 = perfectly stable spending
  - 1 = highly variable month-to-month
- **Wage Stability** (user input or inference)
  - 0 = unstable/freelance income
  - 1 = stable salaried job

**Logic**:
- **High stability** (low volatility + stable wage) → **Index Fund recommended** (14.49% growth)
  - Reason: You can handle market volatility
- **Moderate stability** → **Flexible recommendation** (Index with caution)
- **Low stability** (high volatility + unstable income) → **NPS recommended** (7.11% stable, tax benefit)
  - Reason: Safe, government-backed, tax-advantaged

**Endpoint**: `POST /blackrock/challenge/v1/risk-profile`

**Request** (JSON):
```json
{
  "principal": 145.0,
  "age": 29,
  "annual_income": 600000.0,
  "expense_volatility": 0.32,
  "wage_stability": 0.85
}
```

**Response**:
```json
{
  "riskProfile": "moderate",
  "recommendedInstrument": "index",
  "confidence": 0.78,
  "expenseVolatility": 0.32,
  "wageStability": 0.85,
  "stabilityScore": 0.68,
  "reasoning": "Moderate stability provides opportunity for Index Fund's higher returns with acceptable risk.",
  "npsAdvantages": [
    "Tax deduction up to ₹2,00,000",
    "Stable 7.11% annual returns",
    "Government-backed pension scheme",
    "Good for risk-averse individuals"
  ],
  "indexAdvantages": [
    "Higher growth potential (14.49% annually)",
    "No investment limits",
    "Better for long-term wealth building",
    "Good for stable income holders"
  ]
}
```

**How to Calculate Expense Volatility** (in your frontend):
```typescript
// JavaScript example
function calculateExpenseVolatility(expenses: number[]): number {
  if (expenses.length < 2) return 0.5; // Default mid-range
  
  const avg = expenses.reduce((a, b) => a + b, 0) / expenses.length;
  const variance = expenses.reduce((sum, x) => sum + (x - avg) ** 2, 0) / expenses.length;
  const stdev = Math.sqrt(variance);
  
  // Coefficient of variation (normalized)
  const cv = stdev / avg;
  return Math.min(1.0, cv); // Clamp to [0, 1]
}

// Example usage:
const expenseVolatility = calculateExpenseVolatility([250, 375, 620, 480]);
```

**Use Case**:
- Personalized instrument recommendation with reasoning
- Educate users on NPS vs Index trade-offs
- Build confidence in their investment choice
- Explain which factors drive the recommendation

---

## Test Automation

### Test Folder Structure

```
BrockLee/
├── BrockLee.Tests/
│   ├── Services/
│   │   ├── TransactionServiceTests.cs       # Unit: Transaction parsing & validation
│   │   ├── FilterServiceTests.cs            # Unit: Q/P/K period rule application
│   │   ├── ReturnsServiceTests.cs           # Unit: Investment return calculations
│   │   └── CacheServiceTests.cs             # Unit: Caching behavior
│   ├── Intergration/
│   │   ├── ControllerIntegrationTests.cs    # Integration: API endpoints
│   │   └── LogServiceIntegrationTests.cs    # Integration: Database logging
│   ├── Helpers/
│   │   ├── TestDbContextFactory.cs          # Creates in-memory DB for tests
│   │   └── TransactionBuilder.cs           # Test data factory
│   └── BrockLee.Tests.csproj                # Test project configuration
```

### .NET Unit & Integration Tests

**Framework**: xUnit + FluentAssertions + Mock HTTP  
**Database**: EF Core In-Memory (no SQLite)

**Test Types Included**:
1. **Unit Tests** (Services)
   - Ceiling/remanent calculation
   - Q period override rules (latest start wins, ties resolved by order)
   - P period addition logic (all matching periods summed)
   - K period grouping by date ranges
   - Tax benefit calculation for NPS
   - Compound interest formula (both NPS & Index rates)
   - Inflation adjustment
   - Caching key generation and TTL behavior

2. **Integration Tests** (Controllers)
   - POST `/transactions:parse` → enriched transactions
   - POST `/transactions:validator` → valid/invalid/duplicate buckets
   - POST `/transactions:filter` → K-period groupings
   - POST `/returns:nps` → NPS projections with tax benefits
   - POST `/returns:index` → Index fund projections
   - GET `/performance` → metrics collection

**Run Tests**:

```bash
# Run all tests
dotnet test BrockLee/BrockLee.Tests/BrockLee.Tests.csproj

# Run only unit tests for TransactionService
dotnet test BrockLee/BrockLee.Tests/BrockLee.Tests.csproj -k "TransactionServiceTests"

# Run with verbose output
dotnet test BrockLee/BrockLee.Tests/BrockLee.Tests.csproj --logger "console;verbosity=detailed"

# Run with coverage report (requires OpenCover)
dotnet test BrockLee/BrockLee.Tests/BrockLee.Tests.csproj /p:CollectCoverage=true
```

### Postman Collection (Manual Testing)

A Postman collection is included for manual API testing:

- **File**: `QA/BrockLee.postman_collection.json`

**Setup**:
1. Import into Postman
2. Set `baseUrl = http://localhost:5477`
3. Run in sequence (requests have dependencies)

**Collection Includes**:
- Parse sample expenses
- Validate transactions
- Apply period rules
- Calculate NPS & Index returns
- Get time-series projections
- Analyze risk profiles
- Check performance metrics
- Verify health endpoint

---

## Example: Complete User Flow

**Scenario**: User is 29, earns ₹50k/month, has 4 expenses with high volatility

### Step 1: Parse Expenses
```bash
curl -X POST http://localhost:5477/blackrock/challenge/v1/transactions:parse \
  -H "Content-Type: application/json" \
  -d '{
    "expenses": [
      {"date": "2023-10-12T20:15:00", "amount": 250},
      {"date": "2023-02-28T15:49:00", "amount": 375},
      {"date": "2023-07-01T21:59:00", "amount": 620},
      {"date": "2023-12-17T08:09:00", "amount": 480}
    ]
  }'
```

### Step 2: Calculate Expense Volatility
```javascript
// Frontend calculates volatility from expense amounts
const amounts = [250, 375, 620, 480];
const avg = 431.25;
const stdev = 155.27;
const volatility = stdev / avg; // 0.36 (moderate volatility)
```

### Step 3: Get Risk Profile Recommendation
```bash
curl -X POST http://localhost:5477/blackrock/challenge/v1/risk-profile \
  -H "Content-Type: application/json" \
  -d '{
    "principal": 145.0,
    "age": 29,
    "annual_income": 600000.0,
    "expense_volatility": 0.36,
    "wage_stability": 0.85
  }'
```

**Response suggests**: Index Fund (70% confidence) - "Stable income + moderate spending volatility"

### Step 4: Get Time-Series Projection (Chosen Instrument)
```bash
curl -X POST http://localhost:5477/blackrock/challenge/v1/timeseries:index \
  -H "Content-Type: application/json" \
  -d '{
    "principal": 145.0,
    "age": 29,
    "annual_income": 600000.0,
    "inflation": 0.055
  }'
```

**Response shows**:
- 2026: ₹145 → Real: ₹145
- 2031: ₹204 → Real: ₹159 (5-year horizon)
- 2040: ₹1,280 → Real: ₹780 (compound growth)
- 2057: ₹12,160 → Real: ₹1,829 (retired, inflation-adjusted)
- **Milestone**: ₹1M reached by 2051 (age 54)

### Step 5: Present to User
- Show **Risk Profile**: "Moderate risk-taker with stable income"
- Show **Recommendation**: "Index Fund (78% confidence)" with reasoning
- Show **Timeline Graph**: Visual growth curve with milestones
- Show **Advantages**: Why Index over NPS in their case

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