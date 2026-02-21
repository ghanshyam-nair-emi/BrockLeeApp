"""
compute.py
----------
Pure financial math functions for the BrockLee auto-savings system.
Constants exported so ml_data.py uses identical rates.
"""

from __future__ import annotations
import math

# ── Constants (exported) ──────────────────────────────────────────────────────
NPS_RATE        = 0.0711
INDEX_RATE      = 0.1449
DEFAULT_INFLATE = 0.055
RETIREMENT_AGE  = 60
MIN_YEARS       = 5


# ── Ceiling / Remanent ────────────────────────────────────────────────────────

def ceiling_100(amount: float) -> float:
    if amount % 100 == 0:
        return float(amount)
    return math.ceil(amount / 100.0) * 100.0


def remanent(amount: float) -> float:
    return ceiling_100(amount) - amount


# ── Compound Interest ─────────────────────────────────────────────────────────

def compound_interest(principal: float, rate: float, years: int) -> float:
    if principal <= 0 or years <= 0:
        return 0.0
    return principal * math.pow(1.0 + rate, years)


# ── Inflation Adjustment ──────────────────────────────────────────────────────

def inflation_adjusted(nominal: float, inflation: float, years: int) -> float:
    if years <= 0:
        return nominal
    return nominal / math.pow(1.0 + inflation, years)


# ── Years to Retirement ───────────────────────────────────────────────────────

def years_to_retirement(age: int) -> int:
    return max(MIN_YEARS, RETIREMENT_AGE - age)


# ── Tax Calculation ───────────────────────────────────────────────────────────

def calculate_tax(income: float) -> float:
    if income <= 0:
        return 0.0
    tax = 0.0
    if income > 1_500_000: tax += (income - 1_500_000) * 0.30
    if income > 1_200_000: tax += (min(income, 1_500_000) - 1_200_000) * 0.20
    if income > 1_000_000: tax += (min(income, 1_200_000) - 1_000_000) * 0.15
    if income >   700_000: tax += (min(income, 1_000_000) -   700_000) * 0.10
    return tax


# ── NPS Tax Benefit ───────────────────────────────────────────────────────────

def nps_tax_benefit(invested: float, annual_income: float) -> float:
    deduction = min(invested, annual_income * 0.10, 200_000.0)
    benefit   = calculate_tax(annual_income) - calculate_tax(annual_income - deduction)
    return max(0.0, benefit)


# ── Full Projections ──────────────────────────────────────────────────────────

def project_nps(
    principal: float,
    age: int,
    annual_income: float,
    inflation: float = DEFAULT_INFLATE
) -> dict:
    years  = years_to_retirement(age)
    fv     = compound_interest(principal, NPS_RATE, years)
    real   = inflation_adjusted(fv, inflation, years)
    tax    = nps_tax_benefit(principal, annual_income)
    return {
        "futureValue": round(fv,             2),
        "realValue":   round(real,           2),
        "taxBenefit":  round(tax,            2),
        "profits":     round(real - principal, 2),
    }


def project_index(
    principal: float,
    age: int,
    inflation: float = DEFAULT_INFLATE
) -> dict:
    years  = years_to_retirement(age)
    fv     = compound_interest(principal, INDEX_RATE, years)
    real   = inflation_adjusted(fv, inflation, years)
    return {
        "futureValue": round(fv,             2),
        "realValue":   round(real,           2),
        "taxBenefit":  0.0,
        "profits":     round(real - principal, 2),
    }


# ── Q / P Period Logic ────────────────────────────────────────────────────────

def apply_q_period(remanent_val: float, q_periods: list[dict], tx_date: str) -> float:
    from datetime import datetime
    tx_dt    = datetime.fromisoformat(tx_date)
    matching = [
        (i, q) for i, q in enumerate(q_periods)
        if datetime.fromisoformat(q["start"]) <= tx_dt <= datetime.fromisoformat(q["end"])
    ]
    if not matching:
        return remanent_val
    best = sorted(
        matching,
        key=lambda x: (-datetime.fromisoformat(x[1]["start"]).timestamp(), x[0])
    )[0]
    return float(best[1]["fixed"])


def apply_p_periods(remanent_val: float, p_periods: list[dict], tx_date: str) -> float:
    from datetime import datetime
    tx_dt = datetime.fromisoformat(tx_date)
    for p in p_periods:
        if datetime.fromisoformat(p["start"]) <= tx_dt <= datetime.fromisoformat(p["end"]):
            remanent_val += float(p["extra"])
    return remanent_val