"""
ml_models.py
------------
Trains, evaluates and serves 5 regression models.
Updated to consume the new 4-tuple return from ml_data.generate_training_data().
"""

from __future__ import annotations

import copy
import numpy as np
from dataclasses import dataclass
from sklearn.linear_model    import LinearRegression, Ridge
from sklearn.preprocessing   import PolynomialFeatures, StandardScaler
from sklearn.pipeline        import Pipeline
from sklearn.ensemble        import RandomForestRegressor, GradientBoostingRegressor
from sklearn.model_selection import train_test_split
from sklearn.metrics         import r2_score, mean_absolute_error

from ml_data import generate_training_data, FEATURE_NAMES


# ── Model descriptor ──────────────────────────────────────────────────────────

@dataclass
class TrainedModel:
    name:        str
    short_name:  str
    pipeline:    object
    r2_nps:      float = 0.0
    r2_index:    float = 0.0
    mae_nps:     float = 0.0
    mae_index:   float = 0.0
    description: str   = ""


# ── Registry ──────────────────────────────────────────────────────────────────

_nps_models:   list[TrainedModel] = []
_index_models: list[TrainedModel] = []
_trained       = False
_data_meta:    dict = {}


def _build_pipelines() -> list[tuple[str, str, object, str]]:
    return [
        (
            "Linear Regression", "LR",
            Pipeline([("scaler", StandardScaler()), ("model", LinearRegression())]),
            "Baseline linear model — fast and interpretable"
        ),
        (
            "Polynomial Regression (deg 2)", "PR2",
            Pipeline([
                ("poly",   PolynomialFeatures(degree=2, include_bias=False)),
                ("scaler", StandardScaler()),
                ("model",  LinearRegression())
            ]),
            "Captures non-linear wealth compounding curves"
        ),
        (
            "Ridge Regression", "Ridge",
            Pipeline([("scaler", StandardScaler()), ("model", Ridge(alpha=10.0))]),
            "L2-regularised — robust to wage/age outliers"
        ),
        (
            "Random Forest", "RF",
            Pipeline([
                ("scaler", StandardScaler()),
                ("model",  RandomForestRegressor(
                    n_estimators=100, max_depth=10,
                    random_state=42, n_jobs=-1))
            ]),
            "Ensemble of 100 decision trees — captures complex interactions"
        ),
        (
            "Gradient Boosting", "GB",
            Pipeline([
                ("scaler", StandardScaler()),
                ("model",  GradientBoostingRegressor(
                    n_estimators=200, learning_rate=0.05,
                    max_depth=4, random_state=42))
            ]),
            "Boosted ensemble — highest accuracy, headline model"
        ),
    ]


def train_all_models(n_synthetic: int = 5000) -> None:
    """
    Trains all 5 models on blended synthetic + Kaggle data.
    Populates _nps_models, _index_models, _data_meta.
    """
    global _trained, _data_meta

    print("[ML] Generating blended training data...")

    # ── Updated: unpack 4-tuple ───────────────────────────────────────────────
    X, y_nps, y_index, meta = generate_training_data(
        n_synthetic=n_synthetic, seed=42)

    _data_meta = meta

    X_train, X_test, yn_train, yn_test, yi_train, yi_test = train_test_split(
        X, y_nps, y_index, test_size=0.2, random_state=42)

    print(f"[ML] Training on {len(X_train)} rows "
          f"({meta['synthetic_samples']} synthetic + "
          f"{meta['kaggle_samples']} kaggle)...")

    for name, short, pipeline_blueprint, desc in _build_pipelines():

        nps_pipe = copy.deepcopy(pipeline_blueprint)
        nps_pipe.fit(X_train, yn_train)
        yn_pred  = nps_pipe.predict(X_test)

        nps_model = TrainedModel(
            name        = name,
            short_name  = short,
            pipeline    = nps_pipe,
            r2_nps      = round(float(r2_score(yn_test, yn_pred)),           4),
            mae_nps     = round(float(mean_absolute_error(yn_test, yn_pred)), 2),
            description = desc
        )

        idx_pipe = copy.deepcopy(pipeline_blueprint)
        idx_pipe.fit(X_train, yi_train)
        yi_pred  = idx_pipe.predict(X_test)

        idx_model = TrainedModel(
            name        = name,
            short_name  = short,
            pipeline    = idx_pipe,
            r2_index    = round(float(r2_score(yi_test, yi_pred)),           4),
            mae_index   = round(float(mean_absolute_error(yi_test, yi_pred)), 2),
            description = desc
        )

        _nps_models.append(nps_model)
        _index_models.append(idx_model)

        print(f"[ML]   {name:35s}  NPS R²={nps_model.r2_nps:.4f}  "
              f"Index R²={idx_model.r2_index:.4f}")

    _trained = True
    print("[ML] All models trained. "
          f"Dataset: {meta['total_samples']} total rows | "
          f"Kaggle: {'yes' if meta['kaggle_loaded'] else 'no (synthetic only)'}.")


def predict_all(
    age:            int,
    monthly_wage:   float,
    inflation:      float,
    total_remanent: float,
    expense_count:  int,
) -> list[dict]:
    if not _trained:
        raise RuntimeError("Models not trained. Call train_all_models() first.")

    years   = max(5, 60 - age)
    avg_rem = total_remanent / max(expense_count, 1)

    X = np.array([[
        age, monthly_wage, inflation,
        total_remanent, float(expense_count),
        float(years), avg_rem,
    ]])

    results = []
    for nps_m, idx_m in zip(_nps_models, _index_models):
        nps_pred   = max(0.0, float(nps_m.pipeline.predict(X)[0]))
        index_pred = max(0.0, float(idx_m.pipeline.predict(X)[0]))

        results.append({
            "modelName":   nps_m.name,
            "shortName":   nps_m.short_name,
            "description": nps_m.description,
            "nps": {
                "predictedValue": round(nps_pred,   2),
                "r2Score":        nps_m.r2_nps,
                "mae":            nps_m.mae_nps,
                "confidence":     round(max(0.0, min(100.0, nps_m.r2_nps   * 100)), 1),
            },
            "index": {
                "predictedValue": round(index_pred, 2),
                "r2Score":        idx_m.r2_index,
                "mae":            idx_m.mae_index,
                "confidence":     round(max(0.0, min(100.0, idx_m.r2_index * 100)), 1),
            }
        })

    return results


def get_best_model_recommendation(predictions: list[dict]) -> dict:
    best = max(
        predictions,
        key=lambda m: (m["nps"]["r2Score"] + m["index"]["r2Score"])
    )
    all_nps   = [m["nps"]["predictedValue"]   for m in predictions]
    all_index = [m["index"]["predictedValue"]  for m in predictions]

    return {
        "bestModel":       best["modelName"],
        "bestModelShort":  best["shortName"],
        "consensusNps":    round(float(np.mean(all_nps)),   2),
        "consensusIndex":  round(float(np.mean(all_index)), 2),
        "npsStdDev":       round(float(np.std(all_nps)),    2),
        "indexStdDev":     round(float(np.std(all_index)),  2),
        "modelAgreement":  _model_agreement(all_nps, all_index),
    }


def _model_agreement(nps_preds: list, index_preds: list) -> str:
    def cv(vals):
        arr  = np.array(vals)
        mean = np.mean(arr)
        return (np.std(arr) / mean * 100) if mean > 0 else 0
    avg_cv = (cv(nps_preds) + cv(index_preds)) / 2
    if avg_cv < 5:  return "HIGH"
    if avg_cv < 15: return "MODERATE"
    return "LOW"