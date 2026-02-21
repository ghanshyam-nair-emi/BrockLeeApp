import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PredictResponse } from '../models';

// SignalR is loaded via CDN in index.html to keep bundle size small
declare const signalR: any;

export interface DashboardRefresh {
  triggerType: string;
  occurredAt:  string;
}

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {

  private connection: any = null;
  private _connected = false;

  // Observables consumed by components
  readonly dashboardRefresh$ = new Subject<DashboardRefresh>();
  readonly predictionResult$ = new Subject<PredictResponse>();

  get connected(): boolean { return this._connected; }

  async start(): Promise<void> {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.signalrUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    // ── Event handlers ────────────────────────────────────────────────────
    this.connection.on('DashboardRefresh', (data: DashboardRefresh) => {
      this.dashboardRefresh$.next(data);
    });

    this.connection.on('PredictionResult', (data: PredictResponse) => {
      this.predictionResult$.next(data);
    });

    this.connection.onreconnecting(() => {
      this._connected = false;
      console.warn('[SignalR] Reconnecting...');
    });

    this.connection.onreconnected(() => {
      this._connected = true;
      console.log('[SignalR] Reconnected.');
    });

    this.connection.onclose(() => {
      this._connected = false;
    });

    try {
      await this.connection.start();
      this._connected = true;
      console.log('[SignalR] Connected to dashboard hub.');
    } catch (err) {
      console.error('[SignalR] Connection failed:', err);
    }
  }

  async joinSubmissionGroup(submissionId: string): Promise<void> {
    if (!this._connected) return;
    await this.connection.invoke('JoinSubmissionGroup', submissionId);
  }

  async leaveSubmissionGroup(submissionId: string): Promise<void> {
    if (!this._connected) return;
    await this.connection.invoke('LeaveSubmissionGroup', submissionId);
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection  = null;
      this._connected  = false;
    }
  }

  ngOnDestroy(): void { this.stop(); }
}