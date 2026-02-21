// ── Expense / Transaction ─────────────────────────────────────────────────────

export interface Expense {
  date:   string;
  amount: number;
}

export interface Transaction {
  date:     string;
  amount:   number;
  ceiling:  number;
  remanent: number;
}

export interface InvalidTransaction extends Transaction {
  message: string;
}

// ── Period types ──────────────────────────────────────────────────────────────

export interface QPeriod { fixed: number; start: string; end: string; }
export interface PPeriod { extra: number; start: string; end: string; }
export interface KPeriod { start: string; end: string; }

// ── Returns ───────────────────────────────────────────────────────────────────

export interface ReturnsRequest {
  name:         string;
  age:          number;
  wage:         number;
  inflation:    number;
  transactions: Transaction[];
  q:            QPeriod[];
  p:            PPeriod[];
  k:            KPeriod[];
}

export interface SavingsByDate {
  start:      string;
  end:        string;
  amount:     number;
  profits:    number;
  taxBenefit: number;
}

export interface ReturnsResponse {
  transactionsTotalAmount:  number;
  transactionsTotalCeiling: number;
  savingsByDates:           SavingsByDate[];
}

export interface ComputeResponse {
  submissionId:     string;
  nps:              ReturnsResponse;
  index:            ReturnsResponse;
  responseTimeMs:   number;
  predictionsAsync: boolean;
}

// ── Parse / Validate / Filter ─────────────────────────────────────────────────

export interface ParseRequest  { expenses: Expense[]; }
export interface ParseResponse {
  transactions:  Transaction[];
  totalRemanent: number;
  totalCeiling:  number;
  totalExpense:  number;
}

export interface ValidatorRequest  { wage: number; transactions: Transaction[]; }
export interface ValidatorResponse {
  valid:      Transaction[];
  invalid:    InvalidTransaction[];
  duplicates: InvalidTransaction[];
}

export interface FilterRequest {
  transactions: Transaction[];
  q: QPeriod[];
  p: PPeriod[];
  k: KPeriod[];
}

export interface FilterResponse {
  valid:    Transaction[];
  invalid:  InvalidTransaction[];
  kGroups?: KGroup[];
}

export interface KGroup {
  start:  string;
  end:    string;
  amount: number;
}

// ── ML Predictions ────────────────────────────────────────────────────────────

export interface PredictRequest {
  age:           number;
  monthlyWage:   number;
  inflation:     number;
  totalRemanent: number;
  expenseCount:  number;
}

export interface SingleModelScore {
  predictedValue: number;
  r2Score:        number;
  mae:            number;
  confidence:     number;
}

export interface ModelPrediction {
  modelName:   string;
  shortName:   string;
  description: string;
  nps:         SingleModelScore;
  index:       SingleModelScore;
}

export interface ConsensusResult {
  bestModel:       string;
  bestModelShort:  string;
  consensusNps:    number;
  consensusIndex:  number;
  npsStdDev:       number;
  indexStdDev:     number;
  modelAgreement:  'HIGH' | 'MODERATE' | 'LOW';
}

export interface FeatureImportance {
  feature:    string;
  importance: number;
}

export interface PredictResponse {
  models:            ModelPrediction[];
  consensus:         ConsensusResult;
  featureImportance: FeatureImportance[];
}

export interface ModelMetadata {
  modelName:   string;
  shortName:   string;
  description: string;
  r2Nps:       number;
  r2Index:     number;
  maeNps:      number;
  maeIndex:    number;
}

// ── Logs ──────────────────────────────────────────────────────────────────────

export interface UserLog {
  id:                 string;
  name:               string;
  age:                number;
  wage:               number;
  annualIncome:       number;
  expenseCount:       number;
  totalExpenseAmount: number;
  totalRemanent:      number;
  npsRealValue:       number;
  indexRealValue:     number;
  taxBenefit:         number;
  yearsToRetirement:  number;
  loggedAt:           string;
  responseTimeMs:     number;
}

export interface LogMetrics {
  totalSubmissions:  number;
  avgAge:            number;
  avgWage:           number;
  avgNpsRealValue:   number;
  avgIndexRealValue: number;
  avgResponseTimeMs: number;
  lastSubmittedAt:   string;
}

// ── Performance ───────────────────────────────────────────────────────────────

export interface PerformanceMetrics {
  time:    string;
  memory:  string;
  threads: number;
}

// ── Time-Series Forecast ─────────────────────────────────────────────────────

export interface TimeSeriesRequest {
  principal:     number;
  age:           number;
  annual_income: number;
  inflation:     number;
}

export interface TimeSeriesEntry {
  yearOffset:    number;
  projectionYear: number;
  age:           number;
  balance:       number;
  nominalValue:  number;
  realValue:     number;
  roi:           number;
  growthRate:    number;
  isRetirement:  boolean;
}

export interface MilestoneEntry {
  threshold:     number;
  projectionYear: number;
  yearOffset:    number;
  age:           number;
  realValue:     number;
  description:   string;
}

export interface TimeSeriesResponse {
  principal:   number;
  startAge:    number;
  instrument:  string;
  timeline:    TimeSeriesEntry[];
  milestones:  MilestoneEntry[];
}

// ── Risk Profile ──────────────────────────────────────────────────────────────

export interface RiskProfileRequest {
  principal:            number;
  age:                  number;
  annual_income:        number;
  expense_volatility:   number;
  wage_stability:       number;
}

export interface RiskProfileResponse {
  riskProfile:           string;
  recommendedInstrument: string;
  confidence:            number;
  expenseVolatility:     number;
  wageStability:         number;
  stabilityScore:        number;
  reasoning:             string;
  npsAdvantages:         string[];
  indexAdvantages:       string[];
}

// ── UI state ──────────────────────────────────────────────────────────────────

export type SubmitState = 'idle' | 'loading' | 'success' | 'error';
export type PredictState = 'idle' | 'waiting' | 'received' | 'error';