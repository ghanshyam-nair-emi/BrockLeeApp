import {
  Component, Input, OnChanges,
  SimpleChanges, AfterViewInit,
  ViewChild, ElementRef, OnDestroy
} from '@angular/core';
import { CommonModule, DecimalPipe, DatePipe } from '@angular/common';
import {
  ComputeResponse, SavingsByDate, PredictResponse,
  TimeSeriesResponse, RiskProfileResponse
} from '../../core/models';
import { TimeSeriesChartComponent } from '../timeseries-chart/timeseries-chart.component';
import { RiskProfileComponent } from '../risk-profile/risk-profile.component';
import { ApiService } from '../../core/services/api.service';

declare const Chart: any;

@Component({
  selector:    'app-results-panel',
  standalone:  true,
  imports:     [
    CommonModule, DecimalPipe, DatePipe,
    TimeSeriesChartComponent, RiskProfileComponent
  ],
  templateUrl: './results-panel.component.html',
  styleUrls:   ['./results-panel.component.scss']
})
export class ResultsPanelComponent implements OnChanges, OnDestroy {

  @Input() result:     ComputeResponse  | null = null;
  @Input() prediction: PredictResponse  | null = null;
  @Input() isLoading:  boolean = false;
  @Input() age:        number = 29;
  @Input() wage:       number = 50000;

  @ViewChild('projectionChart')
  chartRef!: ElementRef<HTMLCanvasElement>;

  activeTab: 'nps' | 'index' = 'nps';
  private chartInstance: any = null;

  // Advanced Features
  timeSeriesDataNps:  TimeSeriesResponse | null = null;
  timeSeriesDataIndex: TimeSeriesResponse | null = null;
  riskProfileData:    RiskProfileResponse | null = null;
  
  loadingTimeSeries = false;
  loadingRiskProfile = false;
  expenseVolatility = 0.3;  // Default, will be calculated from expenses

  constructor(private api: ApiService) {}

  // ── Getters ──────────────��────────────────────────────────────────────────

  get currentSavings(): SavingsByDate[] {
    if (!this.result) return [];
    return this.activeTab === 'nps'
      ? this.result.nps.savingsByDates
      : this.result.index.savingsByDates;
  }

  totalRealValue(savings: SavingsByDate[]): number {
    return savings.reduce((s, d) => s + d.profits + d.amount, 0);
  }

  totalProfits(savings: SavingsByDate[]): number {
    return savings.reduce((s, d) => s + d.profits, 0);
  }

  totalTaxBenefit(savings: SavingsByDate[]): number {
    return savings.reduce((s, d) => s + d.taxBenefit, 0);
  }

  setTab(tab: 'nps' | 'index'): void {
    this.activeTab = tab;
    this.renderChart();
  }

  // ── Advanced Features ─────────────────────────────────────────────────────

  loadTimeSeries(isNps: boolean = true): void {
    if (!this.result) return;

    const principal = this.result.nps.savingsByDates.reduce((sum, s) => sum + s.amount, 0);
    const annualIncome = this.wage * 12;

    this.loadingTimeSeries = true;

    this.api.timeseries(
      { principal, age: this.age, annual_income: annualIncome, inflation: 0.055 },
      isNps
    ).subscribe({
      next: (data) => {
        if (isNps) {
          this.timeSeriesDataNps = data;
        } else {
          this.timeSeriesDataIndex = data;
        }
        this.loadingTimeSeries = false;
      },
      error: (err) => {
        console.error('Time-series load failed:', err);
        this.loadingTimeSeries = false;
      }
    });
  }

  loadRiskProfile(): void {
    if (!this.result) return;

    const principal = this.result.nps.savingsByDates.reduce((sum, s) => sum + s.amount, 0);
    const annualIncome = this.wage * 12;

    this.loadingRiskProfile = true;

    this.api.riskProfile({
      principal,
      age: this.age,
      annual_income: annualIncome,
      expense_volatility: this.expenseVolatility,
      wage_stability: 0.8  // Default, could be user-provided
    }).subscribe({
      next: (data) => {
        this.riskProfileData = data;
        this.loadingRiskProfile = false;
      },
      error: (err) => {
        console.error('Risk profile load failed:', err);
        this.loadingRiskProfile = false;
      }
    });
  }

  calculateExpenseVolatility(expenses: any[]): number {
    if (!expenses || expenses.length < 2) return 0.5;

    const amounts = expenses.map((e: any) => e.amount || 0);
    const avg = amounts.reduce((a, b) => a + b, 0) / amounts.length;

    if (avg === 0) return 0.5;

    const variance = amounts.reduce((sum, x) => sum + (x - avg) ** 2, 0) / amounts.length;
    const stdev = Math.sqrt(variance);
    const cv = stdev / avg;

    return Math.min(1.0, cv);
  }

  onInstrumentSelected(instrument: string): void {
    console.log('Instrument selected:', instrument);
    // Can emit event or trigger action
  }

  // ── Lifecycle ─────────────────────────────────────────────────────────────

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['result'] || changes['prediction']) {
      // Defer until view is painted
      setTimeout(() => this.renderChart(), 50);

      // Reset advanced feature data when new results arrive
      if (changes['result'] && changes['result'].currentValue !== changes['result'].previousValue) {
        this.timeSeriesDataNps   = null;
        this.timeSeriesDataIndex = null;
        this.riskProfileData     = null;
        this.loadingTimeSeries   = false;
        this.loadingRiskProfile  = false;
      }
    }
  }

  ngOnDestroy(): void {
    this.destroyChart();
  }

  // ── Chart ─────────────────────────────────────────────────────────────────

  private destroyChart(): void {
    if (this.chartInstance) {
      this.chartInstance.destroy();
      this.chartInstance = null;
    }
  }

  private renderChart(): void {
    if (!this.result || !this.chartRef?.nativeElement) return;

    this.destroyChart();

    const canvas = this.chartRef.nativeElement;
    const ctx    = canvas.getContext('2d');
    if (!ctx) return;

    const { labels, npsData, indexData, years } =
      this.buildProjectionCurves();

    const consensusNps   = this.prediction?.consensus?.consensusNps   ?? null;
    const consensusIndex = this.prediction?.consensus?.consensusIndex ?? null;

    // ── Datasets ──────────────────────────────────────────────────────────

    const datasets: any[] = [
      {
        label:           'NPS Growth (Computed)',
        data:            npsData,
        borderColor:     '#6366f1',
        backgroundColor: 'rgba(99,102,241,0.08)',
        borderWidth:     2,
        pointRadius:     3,
        pointHoverRadius:6,
        fill:            true,
        tension:         0.4,
        yAxisID:         'y'
      },
      {
        label:           'Index Fund Growth (Computed)',
        data:            indexData,
        borderColor:     '#22c55e',
        backgroundColor: 'rgba(34,197,94,0.06)',
        borderWidth:     2,
        pointRadius:     3,
        pointHoverRadius:6,
        fill:            true,
        tension:         0.4,
        yAxisID:         'y'
      }
    ];

    // ML consensus lines — flat horizontal reference
    if (consensusNps !== null) {
      datasets.push({
        label:       `ML Consensus NPS (₹${Math.round(consensusNps).toLocaleString()})`,
        data:        labels.map(() => consensusNps),
        borderColor: '#a5b4fc',
        borderWidth: 1.5,
        borderDash:  [6, 4],
        pointRadius: 0,
        fill:        false,
        tension:     0,
        yAxisID:     'y'
      });
    }

    if (consensusIndex !== null) {
      datasets.push({
        label:       `ML Consensus Index (₹${Math.round(consensusIndex).toLocaleString()})`,
        data:        labels.map(() => consensusIndex),
        borderColor: '#86efac',
        borderWidth: 1.5,
        borderDash:  [6, 4],
        pointRadius: 0,
        fill:        false,
        tension:     0,
        yAxisID:     'y'
      });
    }

    // ── Chart.js config ───────────────────────────────────────────────────

    this.chartInstance = new Chart(ctx, {
      type: 'line',
      data: { labels, datasets },
      options: {
        responsive:          true,
        maintainAspectRatio: false,
        animation:           { duration: 600, easing: 'easeInOutQuart' },
        interaction: {
          mode:      'index',
          intersect: false
        },
        plugins: {
          legend: {
            display:  true,
            position: 'top',
            labels: {
              color:     '#9ca3af',
              font:      { size: 11 },
              boxWidth:  24,
              padding:   16,
              usePointStyle: true,
              pointStyleWidth: 10
            }
          },
          tooltip: {
            backgroundColor: '#1a1d27',
            borderColor:     '#252836',
            borderWidth:     1,
            titleColor:      '#e8eaf0',
            bodyColor:       '#9ca3af',
            padding:         10,
            callbacks: {
              label: (ctx: any) => {
                const v = ctx.parsed.y;
                return ` ${ctx.dataset.label}: ₹${v.toLocaleString('en-IN', {
                  minimumFractionDigits: 2, maximumFractionDigits: 2
                })}`;
              }
            }
          }
        },
        scales: {
          x: {
            grid:    { color: '#252836' },
            ticks: {
              color:    '#5c6272',
              font:     { size: 10 },
              maxTicksLimit: 12
            },
            title: {
              display: true,
              text:    'Year',
              color:   '#5c6272',
              font:    { size: 11 }
            }
          },
          y: {
            grid:    { color: '#252836' },
            ticks: {
              color: '#5c6272',
              font:  { size: 10 },
              callback: (v: number) => {
                if (v >= 1_00_000)
                  return `₹${(v / 1_00_000).toFixed(1)}L`;
                if (v >= 1_000)
                  return `₹${(v / 1_000).toFixed(1)}K`;
                return `₹${v}`;
              }
            },
            title: {
              display: true,
              text:    'Real Value at Retirement (₹)',
              color:   '#5c6272',
              font:    { size: 11 }
            }
          }
        }
      }
    });
  }

  // ── Projection curve builder ──────────────────────────────────────────────
  // Simulates year-by-year compound growth using the same rates as Python:
  //   NPS:   7.11%   annual
  //   Index: 14.49%  annual
  // Inflation-adjusts each year's value so chart shows REAL value

  private buildProjectionCurves(): {
    labels:    string[];
    npsData:   number[];
    indexData: number[];
    years:     number;
  } {
    const NPS_RATE   = 0.0711;
    const INDEX_RATE = 0.1449;

    // Derive total invested from the largest K period (full year)
    const npsSavings   = this.result!.nps.savingsByDates;
    const indexSavings = this.result!.index.savingsByDates;

    // Use the K period with the most data (longest date range = full year)
    const headline = [...npsSavings].sort(
      (a, b) =>
        (new Date(b.end).getTime() - new Date(b.start).getTime()) -
        (new Date(a.end).getTime() - new Date(a.start).getTime())
    )[0];

    const principal   = headline?.amount ?? 0;
    const totalYears  = Math.max(
      ...npsSavings.map(s =>
        Math.ceil(
          (new Date(s.end).getTime() - new Date(s.start).getTime()) /
          (365.25 * 24 * 3600 * 1000)
        )
      ),
      5
    );

    // Infer years to retirement from the computed real value
    // real = principal × (1+r)^t / (1+inf)^t
    // We use 31 as default (age 29 → 60 - 29 = 31) but derive from data
    const npsReal    = (headline?.profits ?? 0) + principal;
    const inflation  = 0.055;

    // Back-calculate years: npsReal = principal × ((1+NPS)/(1+inf))^t
    const effectiveNps   = (1 + NPS_RATE)   / (1 + inflation);
    const effectiveIndex = (1 + INDEX_RATE)  / (1 + inflation);

    let derivedYears = 31; // default
    if (principal > 0 && npsReal > principal) {
      derivedYears = Math.round(
        Math.log(npsReal / principal) / Math.log(effectiveNps)
      );
      derivedYears = Math.max(5, Math.min(derivedYears, 40));
    }

    // Build year-by-year curves
    const labels:    string[] = [];
    const npsData:   number[] = [];
    const indexData: number[] = [];

    const currentYear = new Date().getFullYear();

    for (let t = 0; t <= derivedYears; t++) {
      labels.push(`${currentYear + t}`);

      // Real value at year t
      const npsReal_t   = principal > 0
        ? principal * Math.pow(effectiveNps,   t)
        : 0;
      const indexReal_t = principal > 0
        ? principal * Math.pow(effectiveIndex, t)
        : 0;

      npsData.push(  Math.round(npsReal_t   * 100) / 100);
      indexData.push(Math.round(indexReal_t * 100) / 100);
    }

    return { labels, npsData, indexData, years: derivedYears };
  }
}