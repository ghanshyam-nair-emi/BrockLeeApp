"""
main.py — updated health endpoint + /data/stats
"""

from __future__ import annotations
from contextlib import asynccontextmanager
from fastapi    import FastAPI, HTTPException
from pydantic   import BaseModel, Field
from typing     import Optional

import compute
import ml_models
from ml_data import get_dataset_stats


@asynccontextmanager
async def lifespan(app: FastAPI):
    ml_models.train_all_models(n_synthetic=5000)
    yield


app = FastAPI(
    title     = "BrockLee Python Compute + ML Sidecar",
    version   = "2.1.0",
    lifespan  = lifespan,
    docs_url  = "/docs",
    redoc_url = "/redoc",
)


# ── Models (same as before) ───────────────────────────────────────────────────

class CeilingRequest(BaseModel):
    amount: float = Field(..., ge=0, lt=500_000)

class CeilingResponse(BaseModel):
    amount: float; ceiling: float; remanent: float

class TaxRequest(BaseModel):
    income: float = Field(..., ge=0)

class TaxResponse(BaseModel):
    income: float; tax: float

class ProjectionRequest(BaseModel):
    principal:     float = Field(..., ge=0)
    age:           int   = Field(..., ge=1, le=59)
    annual_income: float = Field(..., gt=0)
    inflation:     float = Field(default=0.055, ge=0, le=1)

class ProjectionResponse(BaseModel):
    futureValue: float; realValue: float
    taxBenefit:  float; profits:   float

class FullProjectionResponse(BaseModel):
    nps: ProjectionResponse; index: ProjectionResponse

class PredictRequest(BaseModel):
    age:            int   = Field(..., ge=1, le=59)
    monthly_wage:   float = Field(..., gt=0)
    inflation:      float = Field(default=0.055, ge=0, le=1)
    total_remanent: float = Field(..., ge=0)
    expense_count:  int   = Field(..., ge=1)

class SingleModelScore(BaseModel):
    predictedValue: float; r2Score: float
    mae: float;            confidence: float

class ModelPrediction(BaseModel):
    modelName: str; shortName: str; description: str
    nps: SingleModelScore; index: SingleModelScore

class ConsensusResult(BaseModel):
    bestModel: str;      bestModelShort: str
    consensusNps: float; consensusIndex: float
    npsStdDev: float;    indexStdDev: float
    modelAgreement: str

class PredictResponse(BaseModel):
    models:            list[ModelPrediction]
    consensus:         ConsensusResult
    featureImportance: Optional[list[dict]] = None

class ModelMetadata(BaseModel):
    modelName: str;  shortName: str; description: str
    r2Nps: float;    r2Index: float
    maeNps: float;   maeIndex: float

class DatasetStats(BaseModel):
    syntheticSamples: int
    kaggleAvailable:  bool
    kaggleRows:       Optional[int]  = None
    kaggleColumns:    Optional[list] = None
    totalTrainRows:   Optional[int]  = None
    kaggleLoaded:     Optional[bool] = None
    usdToInr:         float
    features:         list[str]


# ── Health ────────────────────────────────────────────────────────────────────

@app.get("/health", tags=["Meta"])
def health():
    meta = ml_models._data_meta
    return {
        "status":           "ok",
        "service":          "brocklee-python-compute",
        "version":          "2.1.0",
        "mlReady":          ml_models._trained,
        "modelCount":       len(ml_models._nps_models),
        "datasetSamples":   meta.get("total_samples", 0),
        "syntheticSamples": meta.get("synthetic_samples", 0),
        "kaggleSamples":    meta.get("kaggle_samples", 0),
        "kaggleLoaded":     meta.get("kaggle_loaded", False),
    }


# ── Dataset stats ─────────────────────────────────────────────────────────────

@app.get("/data/stats", response_model=DatasetStats, tags=["Meta"])
def data_stats() -> DatasetStats:
    """
    Returns info about the training data composition.
    Shows how many rows came from Kaggle vs synthetic.
    """
    raw   = get_dataset_stats()
    meta  = ml_models._data_meta

    return DatasetStats(
        syntheticSamples = raw.get("syntheticSamples", 5000),
        kaggleAvailable  = raw.get("kaggleAvailable",  False),
        kaggleRows       = raw.get("kaggleRows"),
        kaggleColumns    = raw.get("kaggleColumns"),
        totalTrainRows   = meta.get("total_samples"),
        kaggleLoaded     = meta.get("kaggle_loaded"),
        usdToInr         = raw.get("usdToInr", 83.5),
        features         = raw.get("features",  []),
    )


# ── Compute endpoints (unchanged) ─────────────────────────────────────────────

@app.post("/compute/ceiling", response_model=CeilingResponse, tags=["Compute"])
def get_ceiling(req: CeilingRequest):
    c = compute.ceiling_100(req.amount)
    return CeilingResponse(amount=req.amount, ceiling=round(c,2), remanent=round(c-req.amount,2))

@app.post("/compute/tax", response_model=TaxResponse, tags=["Compute"])
def get_tax(req: TaxRequest):
    return TaxResponse(income=req.income, tax=round(compute.calculate_tax(req.income),2))

@app.post("/compute/nps", response_model=ProjectionResponse, tags=["Compute"])
def project_nps(req: ProjectionRequest):
    return ProjectionResponse(**compute.project_nps(req.principal, req.age, req.annual_income, req.inflation))

@app.post("/compute/index", response_model=ProjectionResponse, tags=["Compute"])
def project_index(req: ProjectionRequest):
    return ProjectionResponse(**compute.project_index(req.principal, req.age, req.inflation))

@app.post("/compute/full", response_model=FullProjectionResponse, tags=["Compute"])
def project_full(req: ProjectionRequest):
    return FullProjectionResponse(
        nps   = ProjectionResponse(**compute.project_nps(  req.principal, req.age, req.annual_income, req.inflation)),
        index = ProjectionResponse(**compute.project_index(req.principal, req.age, req.inflation)),
    )


# ── ML endpoints ──────────────────────────────────────────────────────────────

@app.post("/predict", response_model=PredictResponse, tags=["ML Predictions"])
def predict(req: PredictRequest):
    if not ml_models._trained:
        raise HTTPException(status_code=503, detail="ML models not ready.")

    try:
        raw = ml_models.predict_all(
            age=req.age, monthly_wage=req.monthly_wage,
            inflation=req.inflation, total_remanent=req.total_remanent,
            expense_count=req.expense_count,
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Prediction error: {e}")

    consensus   = ml_models.get_best_model_recommendation(raw)
    importance  = _get_feature_importance()

    return PredictResponse(
        models            = [ModelPrediction(**m) for m in raw],
        consensus         = ConsensusResult(**consensus),
        featureImportance = importance,
    )


@app.get("/predict/models", response_model=list[ModelMetadata], tags=["ML Predictions"])
def get_model_metadata():
    if not ml_models._trained:
        raise HTTPException(status_code=503, detail="Models not ready.")
    return [
        ModelMetadata(
            modelName   = n.name,  shortName   = n.short_name,
            description = n.description,
            r2Nps       = n.r2_nps,  r2Index   = i.r2_index,
            maeNps      = n.mae_nps, maeIndex  = i.mae_index,
        )
        for n, i in zip(ml_models._nps_models, ml_models._index_models)
    ]


def _get_feature_importance() -> list[dict]:
    from ml_data import FEATURE_NAMES
    try:
        rf = ml_models._nps_models[3]
        imps = rf.pipeline.named_steps["model"].feature_importances_
        return [
            {"feature": name, "importance": round(float(imp), 4)}
            for name, imp in sorted(
                zip(FEATURE_NAMES, imps), key=lambda x: x[1], reverse=True)
        ]
    except Exception:
        return []


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=False)