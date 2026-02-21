import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ParseRequest, ParseResponse,
  ValidatorRequest, ValidatorResponse,
  FilterRequest, FilterResponse,
  ReturnsRequest, ReturnsResponse,
  ComputeResponse, PredictRequest,
  PredictResponse, ModelMetadata,
  UserLog, LogMetrics, PerformanceMetrics
} from '../models';

@Injectable({ providedIn: 'root' })
export class ApiService {

  private readonly base    = environment.apiUrl;
  private readonly api     = `${this.base}/blackrock/challenge/v1`;
  private readonly headers = new HttpHeaders({
    'Content-Type': 'application/json',
    'Accept':       'application/json'
  });

  constructor(private http: HttpClient) {}

  // ── Transactions ──────────────────────────────────────────────────────────

  parse(req: ParseRequest): Observable<ParseResponse> {
    return this.http.post<ParseResponse>(
      `${this.api}/transactions:parse`, req, { headers: this.headers });
  }

  validate(req: ValidatorRequest): Observable<ValidatorResponse> {
    return this.http.post<ValidatorResponse>(
      `${this.api}/transactions:validator`, req, { headers: this.headers });
  }

  filter(req: FilterRequest): Observable<FilterResponse> {
    return this.http.post<FilterResponse>(
      `${this.api}/transactions:filter`, req, { headers: this.headers });
  }

  // ── Returns ───────────────────────────────────────────────────────────────

  nps(req: ReturnsRequest): Observable<ReturnsResponse> {
    return this.http.post<ReturnsResponse>(
      `${this.api}/returns:nps`, req, { headers: this.headers });
  }

  index(req: ReturnsRequest): Observable<ReturnsResponse> {
    return this.http.post<ReturnsResponse>(
      `${this.api}/returns:index`, req, { headers: this.headers });
  }

  compute(req: ReturnsRequest): Observable<ComputeResponse> {
    return this.http.post<ComputeResponse>(
      `${this.api}/returns:compute`, req, { headers: this.headers });
  }

  // ── Predictions ───────────────────────────────────────────────────────────

  predict(req: PredictRequest): Observable<PredictResponse> {
    return this.http.post<any>(
      `${this.api}/predictions`, req, { headers: this.headers })
      .pipe(
        map(res => {
          // Debug: log raw response so we can see exact shape
          console.debug('[API] Raw /predictions response:', res);
          const normalised = this.normalisePredictResponse(res);
          console.debug('[API] Normalised prediction:', normalised);
          return normalised;
        })
      );
  }

  getModelMetadata(): Observable<ModelMetadata[]> {
    return this.http.get<ModelMetadata[]>(
      `${this.api}/predictions/models`, { headers: this.headers });
  }

  // ── Logs ─────────────────────────────────────────────────────────���────────

  getLogs(count = 50): Observable<UserLog[]> {
    return this.http.get<UserLog[]>(
      `${this.api}/logs?count=${count}`, { headers: this.headers });
  }

  getMetrics(): Observable<LogMetrics> {
    return this.http.get<LogMetrics>(
      `${this.api}/logs/metrics`, { headers: this.headers });
  }

  // ── Performance ───────────────────────────────────────────────────────────

  getPerformance(): Observable<PerformanceMetrics> {
    return this.http.get<PerformanceMetrics>(
      `${this.api}/performance`, { headers: this.headers });
  }

  // ── Normalise predict response ─────────────────────────────────────────────
  // Handles both camelCase (.NET serialised) and snake_case (Python direct)
  // Also handles PredictResultMessage shape (SignalR summary — has no models[])

  private normalisePredictResponse(res: any): PredictResponse {
    // If res has no models array at all → empty safe response
    const rawModels = res?.models ?? [];

    const models = rawModels.map((m: any) => ({
      modelName:   m?.modelName   ?? m?.model_name   ?? '—',
      shortName:   m?.shortName   ?? m?.short_name   ?? '—',
      description: m?.description ?? '',
      nps: {
        predictedValue: +(m?.nps?.predictedValue ?? m?.nps?.predicted_value ?? 0),
        r2Score:        +(m?.nps?.r2Score        ?? m?.nps?.r2_score        ?? 0),
        mae:            +(m?.nps?.mae            ?? 0),
        confidence:     +(m?.nps?.confidence     ?? 0),
      },
      index: {
        predictedValue: +(m?.index?.predictedValue ?? m?.index?.predicted_value ?? 0),
        r2Score:        +(m?.index?.r2Score        ?? m?.index?.r2_score        ?? 0),
        mae:            +(m?.index?.mae            ?? 0),
        confidence:     +(m?.index?.confidence     ?? 0),
      }
    }));

    const con = res?.consensus ?? {};
    const consensus = {
      bestModel:      con?.bestModel      ?? con?.best_model      ?? '—',
      bestModelShort: con?.bestModelShort ?? con?.best_model_short ?? '—',
      consensusNps:   +(con?.consensusNps   ?? con?.consensus_nps   ?? 0),
      consensusIndex: +(con?.consensusIndex ?? con?.consensus_index ?? 0),
      npsStdDev:      +(con?.npsStdDev      ?? con?.nps_std_dev     ?? 0),
      indexStdDev:    +(con?.indexStdDev    ?? con?.index_std_dev   ?? 0),
      modelAgreement:  con?.modelAgreement  ?? con?.model_agreement ?? 'LOW',
    };

    const featureImportance = (
      res?.featureImportance ?? res?.feature_importance ?? []
    ).map((f: any) => ({
      feature:    f?.feature    ?? '',
      importance: +(f?.importance ?? 0),
    }));

    return { models, consensus, featureImportance };
  }
}