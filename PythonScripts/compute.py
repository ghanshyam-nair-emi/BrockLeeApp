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


# ── Time-Series Forecast (Year-by-Year Progression) ──────────────────────────

def project_timeline(
    principal: float,
    age: int,
    annual_income: float,
    is_nps: bool = True,
    inflation: float = DEFAULT_INFLATE
) -> list[dict]:
    """
    Generate year-by-year progression from now until retirement.
    Returns list of dicts with year, balance, nominal/real value, ROI.
    """
    if principal <= 0:
        return []

    years_total = years_to_retirement(age)
    retirement_year = 2026 + years_total  # Assuming current year 2026
    
    timeline = []
    rate = NPS_RATE if is_nps else INDEX_RATE
    
    for year_offset in range(years_total + 1):
        # Compound growth
        fv_nominal = compound_interest(principal, rate, year_offset)
        fv_real = inflation_adjusted(fv_nominal, inflation, year_offset)
        
        roi = fv_real - principal
        
        timeline.append({
            "yearOffset": year_offset,
            "projectionYear": 2026 + year_offset,
            "age": age + year_offset,
            "balance": round(principal, 2),
            "nominalValue": round(fv_nominal, 2),
            "realValue": round(fv_real, 2),
            "roi": round(roi, 2),
            "growthRate": round(rate, 4) if year_offset > 0 else 0.0,
            "isRetirement": year_offset == years_total
        })
    
    return timeline


def project_milestones(
    principal: float,
    age: int,
    annual_income: float,
    is_nps: bool = True,
    inflation: float = DEFAULT_INFLATE
) -> list[dict]:
    """
    Extract key milestones (specific balance thresholds) from timeline.
    E.g., when do you hit ₹100k, ₹500k, etc.?
    """
    timeline = project_timeline(principal, age, annual_income, is_nps, inflation)
    
    milestones = []
    thresholds = [100_000, 500_000, 1_000_000, 2_000_000, 5_000_000]
    
    for threshold in thresholds:
        for entry in timeline:
            if entry["realValue"] >= threshold:
                milestones.append({
                    "threshold": threshold,
                    "projectionYear": entry["projectionYear"],
                    "yearOffset": entry["yearOffset"],
                    "age": entry["age"],
                    "realValue": entry["realValue"],
                    "description": f"₹{threshold:,} milestone reached"
                })
                break  # Only first crossing per threshold
    
    return milestones


# ── Risk Profiling (Expense-Based) ────────────────────────────────────────────

def calculate_expense_volatility(expenses: list[dict]) -> float:
    """
    Calculate expense volatility (coefficient of variation).
    Input: list of {"date": ..., "amount": ...}
    Returns: volatility between 0 and 1 (0=perfectly stable, 1=highly volatile)
    """
    if not expenses or len(expenses) < 2:
        return 0.5  # Default mid-range for insufficient data
    
    amounts = [float(e.get("amount", 0)) for e in expenses]
    avg = sum(amounts) / len(amounts)
    
    if avg == 0:
        return 0.5
    
    variance = sum((x - avg) ** 2 for x in amounts) / len(amounts)
    stdev = math.sqrt(variance)
    
    # Coefficient of variation (normalized by mean)
    cv = stdev / avg
    
    # Clamp to [0, 1] range for easier interpretation
    return min(1.0, cv)


def profile_risk(
    principal: float,
    age: int,
    annual_income: float,
    expense_volatility: float,
    wage_stability: float = 0.8  # Assume stable unless told otherwise
) -> dict:
    """
    ML-based risk profile: recommend NPS or Index based on expense patterns.
    
    Input:
      - expense_volatility: 0 (stable) to 1 (volatile) [calculated from expense amounts]
      - wage_stability: 0 (unstable job) to 1 (stable job) [user input or inferred]
    
    Returns:
      - recommendedInstrument: "nps" or "index"
      - riskProfile: "conservative" | "moderate" | "aggressive"
      - confidence: 0-1
      - reasoning: explanation
    """
    
    # Risk scoring logic (simple heuristic, could be ML-driven)
    # High volatility + low wage stability → NPS (safer)
    # Low volatility + high wage stability → Index (growth)
    
    stability_score = (1.0 - expense_volatility) * 0.5 + wage_stability * 0.5
    
    if stability_score > 0.75:
        recommended = "index"
        risk_profile = "aggressive"
        confidence = 0.82
        reasoning = "Stable income + consistent spending patterns suggest capacity for higher-growth Index Fund."
    elif stability_score > 0.5:
        recommended = "index"
        risk_profile = "moderate"
        confidence = 0.70
        reasoning = "Moderate stability provides opportunity for Index Fund's higher returns with acceptable risk."
    else:
        recommended = "nps"
        risk_profile = "conservative"
        confidence = 0.75
        reasoning = "Variable expense patterns suggest NPS's stable, tax-advantaged returns are safer."
    
    return {
        "riskProfile": risk_profile,
        "recommendedInstrument": recommended,
        "confidence": round(confidence, 2),
        "expenseVolatility": round(expense_volatility, 2),
        "wageStability": round(wage_stability, 2),
        "stabilityScore": round(stability_score, 2),
        "reasoning": reasoning,
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