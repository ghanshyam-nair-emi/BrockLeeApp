"""
ml_data.py
----------
Generates and blends training data for BrockLee ML models.

Two sources:
  1. SYNTHETIC  — 5000 algorithmically generated Indian salary profiles.
  2. KAGGLE     — Real salary dataset loaded from data/salary_data.csv.
                  Gracefully skipped if file absent or malformed.
"""

from __future__ import annotations

import os
import numpy as np
import pandas as pd
from pathlib import Path

from compute import (
    compound_interest,
    inflation_adjusted,
    years_to_retirement,
    NPS_RATE,
    INDEX_RATE,
)

# ── Constants ─────────────────────────────────────────────────────────────────

DATA_DIR   = Path(__file__).parent / "data"
KAGGLE_CSV = DATA_DIR / "salary_data.csv"

FEATURE_NAMES = [
    "age",
    "monthly_wage",
    "inflation",
    "total_remanent",
    "expense_count",
    "years_to_retirement",
    "avg_remanent_per_expense",
]

USD_TO_INR = 83.5

EDUCATION_MULTIPLIERS = {
    "high school":  0.75,
    "bachelor's":   1.00,
    "bachelor":     1.00,
    "master's":     1.25,
    "master":       1.25,
    "phd":          1.50,
    "doctorate":    1.50,
}


# ── Target computation ────────────────────────────────────────────────────────

def _compute_targets(
    ages:            np.ndarray,
    total_remanents: np.ndarray,
    inflations:      np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    n       = len(ages)
    y_nps   = np.zeros(n)
    y_index = np.zeros(n)

    for i in range(n):
        p   = float(total_remanents[i])
        t   = years_to_retirement(int(ages[i]))
        inf = float(inflations[i])

        nps_fv     = compound_interest(p, NPS_RATE,  t)
        idx_fv     = compound_interest(p, INDEX_RATE, t)
        y_nps[i]   = inflation_adjusted(nps_fv, inf, t)
        y_index[i] = inflation_adjusted(idx_fv, inf, t)

    return y_nps, y_index


# ── Source 1: Synthetic ───────────────────────────────────────────────────────

def _generate_synthetic(
    n_samples: int = 5000,
    seed:      int = 42,
) -> pd.DataFrame:
    rng = np.random.default_rng(seed)

    ages = rng.integers(22, 60, size=n_samples).astype(float)

    monthly_wages = np.clip(
        rng.lognormal(mean=10.8, sigma=0.8, size=n_samples),
        a_min=15_000, a_max=500_000
    )

    inflations     = rng.uniform(0.03, 0.09, size=n_samples)
    expense_counts = rng.integers(1, 200, size=n_samples).astype(float)

    avg_remanent = np.clip(
        rng.lognormal(mean=3.5, sigma=1.0, size=n_samples),
        a_min=0, a_max=500
    )
    total_remanents = avg_remanent * expense_counts
    years           = np.array([years_to_retirement(int(a)) for a in ages])

    return pd.DataFrame({
        "age":                      ages,
        "monthly_wage":             monthly_wages,
        "inflation":                inflations,
        "total_remanent":           total_remanents,
        "expense_count":            expense_counts,
        "years_to_retirement":      years,
        "avg_remanent_per_expense": avg_remanent,
        "source":                   "synthetic",
    })


# ── Source 2: Kaggle ──────────────────────────────────────────────────────────

def _load_kaggle(seed: int = 42) -> pd.DataFrame | None:
    if not KAGGLE_CSV.exists():
        print(f"[ML Data] Kaggle CSV not found at {KAGGLE_CSV}. "
              f"Using synthetic data only.")
        return None

    print(f"[ML Data] Loading Kaggle dataset from {KAGGLE_CSV}...")

    try:
        df = pd.read_csv(KAGGLE_CSV)
    except Exception as e:
        print(f"[ML Data] Failed to read CSV: {e}. Using synthetic only.")
        return None

    # ── Normalise column names ────────────────────────────────────────────────
    df.columns = df.columns.str.strip().str.lower()

    required = {"age", "salary"}
    missing  = required - set(df.columns)
    if missing:
        print(f"[ML Data] Missing required columns: {missing}. Using synthetic only.")
        return None

    print(f"[ML Data] Kaggle raw rows: {len(df)}")

    # ── Age ───────────────────────────────────────────────────────────────────
    df["age"] = pd.to_numeric(df["age"], errors="coerce")
    df         = df.dropna(subset=["age"])
    df["age"]  = df["age"].clip(22, 59).astype(float)

    # ── Salary → monthly wage in INR ─────────────────────────────────────────
    df["salary"] = pd.to_numeric(df["salary"], errors="coerce")
    df            = df.dropna(subset=["salary"])
    df            = df[df["salary"] > 0]

    median_salary = df["salary"].median()
    if median_salary > 5_000:
        df["monthly_wage"] = (df["salary"] / 12) * USD_TO_INR
    else:
        df["monthly_wage"] = df["salary"] * USD_TO_INR

    df["monthly_wage"] = df["monthly_wage"].clip(15_000, 500_000)

    # ── Education level → wage multiplier ────────────────────────────────────
    # FIX: guard against NaN / non-string values before calling `k in x`
    if "education level" in df.columns:
        def get_edu_multiplier(x) -> float:
            # NaN comes in as float — must check type before string ops
            if not isinstance(x, str) or x.strip() == "":
                return 1.0
            x_lower = x.strip().lower()
            return next(
                (v for k, v in EDUCATION_MULTIPLIERS.items() if k in x_lower),
                1.0   # default if education level not recognised
            )

        df["edu_mult"]     = df["education level"].map(get_edu_multiplier)
        df["monthly_wage"] = (
            df["monthly_wage"] * df["edu_mult"]
        ).clip(15_000, 500_000)

    # ── Years of Experience → expense_count proxy ─────────────────────────────
    rng = np.random.default_rng(seed)

    if "years of experience" in df.columns:
        df["years_exp"] = pd.to_numeric(
            df["years of experience"], errors="coerce"
        ).fillna(3.0).clip(0, 40)

        tx_per_year         = rng.integers(8, 20, size=len(df))
        df["expense_count"] = (
            df["years_exp"] * tx_per_year
        ).clip(1, 500).astype(float)
    else:
        df["expense_count"] = rng.integers(10, 100, size=len(df)).astype(float)

    # ── Inflation (not in Kaggle — derive stochastically) ────────────────────
    df["inflation"] = rng.uniform(0.03, 0.09, size=len(df))

    # ── Remanent (not in Kaggle — derive from wage) ───────────────────────────
    wage_factor               = (
        df["monthly_wage"] / df["monthly_wage"].max()
    ).values
    base_remanent             = rng.lognormal(3.5, 0.8, size=len(df))
    df["avg_remanent_per_expense"] = np.clip(
        base_remanent * (1 + wage_factor), 0, 500
    )
    df["total_remanent"] = (
        df["avg_remanent_per_expense"] * df["expense_count"]
    ).clip(0)

    # ── Years to retirement ───────────────────────────────────────────────────
    df["years_to_retirement"] = df["age"].apply(
        lambda a: float(years_to_retirement(int(a)))
    )

    # ── Final column selection + rename to canonical names ────────────────────
    result = df[[
        "age",
        "monthly_wage",
        "inflation",
        "total_remanent",
        "expense_count",
        "years_to_retirement",
        "avg_remanent_per_expense",
    ]].copy()

    result.columns   = FEATURE_NAMES
    result["source"] = "kaggle"

    # ── Final null check — drop any remaining NaNs ────────────────────────────
    before = len(result)
    result = result.dropna(subset=FEATURE_NAMES)
    after  = len(result)

    if before != after:
        print(f"[ML Data] Dropped {before - after} rows with remaining nulls.")

    print(f"[ML Data] Kaggle cleaned rows: {len(result)}")
    return result


# ── Blend and export ──────────────────────────────────────────────────────────

def generate_training_data(
    n_synthetic: int = 5000,
    seed:        int = 42,
) -> tuple[np.ndarray, np.ndarray, np.ndarray, dict]:
    synthetic_df = _generate_synthetic(n_samples=n_synthetic, seed=seed)
    kaggle_df    = _load_kaggle(seed=seed)

    if kaggle_df is not None and len(kaggle_df) > 0:
        combined = pd.concat(
            [synthetic_df, kaggle_df],
            ignore_index=True
        )
        n_kaggle = len(kaggle_df)
    else:
        combined = synthetic_df
        n_kaggle = 0

    combined = combined.sample(frac=1, random_state=seed).reset_index(drop=True)

    X = combined[FEATURE_NAMES].values.astype(float)

    y_nps, y_index = _compute_targets(
        ages            = X[:, 0],
        total_remanents = X[:, 3],
        inflations      = X[:, 2],
    )

    meta = {
        "total_samples":     len(X),
        "synthetic_samples": n_synthetic,
        "kaggle_samples":    n_kaggle,
        "kaggle_loaded":     n_kaggle > 0,
        "feature_names":     FEATURE_NAMES,
    }

    print(
        f"[ML Data] Final dataset: {len(X)} rows "
        f"({n_synthetic} synthetic + {n_kaggle} kaggle)"
    )

    return X, y_nps, y_index, meta


# ── Dataset stats ─────────────────────────────────────────────────────────────

def get_dataset_stats() -> dict:
    kaggle_exists = KAGGLE_CSV.exists()
    stats = {
        "syntheticSamples": 5000,
        "kaggleAvailable":  kaggle_exists,
        "kagglePath":       str(KAGGLE_CSV),
        "features":         FEATURE_NAMES,
        "usdToInr":         USD_TO_INR,
    }
    if kaggle_exists:
        try:
            df = pd.read_csv(KAGGLE_CSV)
            stats["kaggleRows"]    = len(df)
            stats["kaggleColumns"] = list(df.columns)
        except Exception:
            stats["kaggleRows"] = 0
    return stats