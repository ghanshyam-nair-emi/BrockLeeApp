import {
  Component, OnInit, OnDestroy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, interval, Subscription } from 'rxjs';
import { takeUntil, switchMap } from 'rxjs/operators';

import { ApiService }    from '../../core/services/api.service';
import { SignalRService } from '../../core/services/signalr.service';

import { UserFormComponent }        from '../user-form/user-form.component';
import { ResultsPanelComponent }    from '../results-panel/results-panel.component';
import { PredictionPanelComponent } from '../prediction-panel/prediction-panel.component';
import { LogTableComponent }        from '../log-table/log-table.component';

import {
  ReturnsRequest, ComputeResponse,
  PredictResponse, UserLog,
  LogMetrics, SubmitState, PredictState
} from '../../core/models';

@Component({
  selector:    'app-dashboard',
  standalone:  true,
  imports:     [
    CommonModule,
    UserFormComponent,
    ResultsPanelComponent,
    PredictionPanelComponent,
    LogTableComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrls:   ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy {

  submitState:  SubmitState  = 'idle';
  predictState: PredictState = 'idle';

  result:     ComputeResponse | null = null;
  prediction: PredictResponse | null = null;
  logs:       UserLog[]              = [];
  metrics:    LogMetrics | null      = null;

  isFormLoading:   boolean = false;
  isLogRefreshing: boolean = false;

  private currentSubmissionId: string | null = null;
  private currentRequest:      ReturnsRequest | null = null;
  private currentResult:       ComputeResponse | null = null;
  private destroy$    = new Subject<void>();
  private logPollSub?: Subscription;

  constructor(
    private api:       ApiService,
    protected signalR: SignalRService
  ) {}

  async ngOnInit(): Promise<void> {
    await this.signalR.start();

    // Dashboard refresh — any user submitted, reload logs
    this.signalR.dashboardRefresh$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.refreshLogs());

    // ── FIX: SignalR delivers PredictResultMessage (summary shape)
    // We ignore it and instead make a direct /predictions HTTP call
    // using the stored request — this guarantees full PredictResponse shape.
    // This is more reliable than trying to map the SignalR message shape.
    this.signalR.predictionResult$
      .pipe(takeUntil(this.destroy$))
      .subscribe(async (_signalRMsg) => {

        // Leave SignalR group — signal received, now fetch full data
        if (this.currentSubmissionId) {
          await this.signalR.leaveSubmissionGroup(this.currentSubmissionId);
          this.currentSubmissionId = null;
        }

        // Fetch full ML prediction via HTTP using stored request
        if (this.currentRequest && this.currentResult) {
          await this.fetchPrediction(this.currentRequest, this.currentResult);
        }
      });

    this.refreshLogs();

    // Fallback poll — 15s, much less frequent since SignalR handles most
    this.logPollSub = interval(15_000)
      .pipe(
        takeUntil(this.destroy$),
        switchMap(() => this.api.getLogs(50))
      )
      .subscribe(logs => { this.logs = logs; });
  }

  // ── Form submitted ─────────────────────────────────────────────────────────

  async onFormSubmitted(request: ReturnsRequest): Promise<void> {
    this.submitState  = 'loading';
    this.predictState = 'waiting';
    this.result       = null;
    this.prediction   = null;
    this.currentRequest = request;

    try {
      const res = await this.api.compute(request).toPromise();
      if (!res) throw new Error('Empty response from compute.');

      this.result       = res;
      this.currentResult = res;
      this.submitState  = 'success';

      // Join SignalR group so we get notified when ML is done
      this.currentSubmissionId = res.submissionId;

      if (this.signalR.connected) {
        await this.signalR.joinSubmissionGroup(res.submissionId);

        // Safety timeout — if SignalR notification never arrives
        // (Python slow, queue lag, network) fall back after 15s
        setTimeout(async () => {
          if (this.predictState === 'waiting') {
            console.warn('[Dashboard] SignalR timeout — using fallback predict.');
            await this.fetchPrediction(request, res);
          }
        }, 15_000);

      } else {
        // SignalR not connected — call directly
        await this.fetchPrediction(request, res);
      }

    } catch (err: any) {
      this.submitState  = 'error';
      this.predictState = 'error';
      console.error('[Dashboard] Compute failed:', err);
    }
  }

  // ── Fetch full PredictResponse via HTTP ───────────────────────────────────
  // Always called with proper shape — no SignalR message mapping needed

  private async fetchPrediction(
    request: ReturnsRequest,
    res:     ComputeResponse
  ): Promise<void> {
    try {
      const totalRemanent = res.nps.savingsByDates
        .reduce((s, d) => s + d.amount, 0);

      const pred = await this.api.predict({
        age:           request.age,
        monthlyWage:   request.wage,
        inflation:     request.inflation,
        totalRemanent,
        expenseCount:  request.transactions.length
      }).toPromise();

      if (pred && pred.consensus && pred.models?.length > 0) {
        this.prediction   = pred;
        this.predictState = 'received';
      } else {
        console.warn('[Dashboard] Predict response missing consensus:', pred);
        this.predictState = 'error';
      }
    } catch (err) {
      console.error('[Dashboard] Prediction HTTP call failed:', err);
      this.predictState = 'error';
    }
  }

  // ── Log refresh ───────────────────────────────────────────────────────────

  async refreshLogs(): Promise<void> {
    this.isLogRefreshing = true;
    try {
      const [logs, metrics] = await Promise.all([
        this.api.getLogs(50).toPromise(),
        this.api.getMetrics().toPromise()
      ]);
      if (logs)    this.logs    = logs;
      if (metrics) this.metrics = metrics;
    } finally {
      this.isLogRefreshing = false;
    }
  }

  onFormLoading(loading: boolean): void {
    this.isFormLoading = loading;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.logPollSub?.unsubscribe();
    this.signalR.stop();
  }
}