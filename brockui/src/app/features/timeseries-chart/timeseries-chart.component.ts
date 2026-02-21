import {
  Component, Input, ViewChild, ElementRef,
  AfterViewInit, OnDestroy, OnChanges
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  Chart, ChartConfiguration,
  CategoryScale, LinearScale,
  LineElement, PointElement, LineController,
  Filler, Legend, Title, Tooltip
} from 'chart.js';
import { TimeSeriesResponse, TimeSeriesEntry, MilestoneEntry } from '../../core/models';

// Register all required Chart.js components once
Chart.register(
  CategoryScale, LinearScale,
  LineElement, PointElement, LineController,
  Filler, Legend, Title, Tooltip
);
// Dark theme defaults
Chart.defaults.color = '#9ca3af';
Chart.defaults.borderColor = '#252836';

@Component({
  selector:    'app-timeseries-chart',
  standalone:  true,
  imports:     [CommonModule],
  templateUrl: './timeseries-chart.component.html',
  styleUrls:   ['./timeseries-chart.component.scss']
})
export class TimeSeriesChartComponent implements AfterViewInit, OnChanges, OnDestroy {

  @Input() data: TimeSeriesResponse | null = null;
  @ViewChild('projectionCanvas', { static: false }) canvasRef!: ElementRef<HTMLCanvasElement>;

  private chartInstance: Chart<'line'> | null = null;
  chartConfig: ChartConfiguration<'line'> | null = null;
  selectedMetric: 'nominal' | 'real' = 'real';
  selectedMilestone: MilestoneEntry | null = null;

  // ngOnInit is intentionally omitted; ngOnChanges handles initial data

  ngAfterViewInit(): void {
    if (this.chartConfig) {
      // data already arrived before view init
      setTimeout(() => this.renderChart(), 0);
    }
  }

  ngOnChanges(): void {
    if (this.data) {
      this.buildChart();
      // defer so Angular finishes rendering the canvas element first
      setTimeout(() => this.renderChart(), 0);
    } else {
      this.destroyChart();
    }
  }

  ngOnDestroy(): void {
    this.destroyChart();
  }

  private destroyChart(): void {
    if (this.chartInstance) {
      this.chartInstance.destroy();
      this.chartInstance = null;
    }
  }

  private renderChart(): void {
    if (!this.chartConfig || !this.canvasRef?.nativeElement) return;
    
    this.destroyChart();
    
    const ctx = this.canvasRef.nativeElement.getContext('2d');
    if (!ctx) return;

    this.chartInstance = new Chart(ctx, this.chartConfig);
  }

  buildChart(): void {
    if (!this.data || !this.data.timeline || this.data.timeline.length === 0) {
      return;
    }

    const timeline = this.data.timeline;
    const years = timeline.map(t => t.projectionYear.toString());
    const nominal = timeline.map(t => Math.round(t.nominalValue));
    const real = timeline.map(t => Math.round(t.realValue));
    const roi = timeline.map(t => Math.round(t.roi));

    // Color coding: red for negative, green for positive
    const roiColors = roi.map(r => r < 0 ? '#dc3545' : '#28a745');

    this.chartConfig = {
      type: 'line',
      data: {
        labels: years,
        datasets: [
          {
            label: `Real Value (Inflation-Adjusted)`,
            data: real,
            borderColor: '#6366f1',
            backgroundColor: 'rgba(99, 102, 241, 0.15)',
            borderWidth: 2,
            fill: true,
            tension: 0.4,
            pointRadius: 2,
            pointHoverRadius: 5
          },
          {
            label: `Nominal Value`,
            data: nominal,
            borderColor: '#5c6272',
            backgroundColor: 'rgba(92, 98, 114, 0.05)',
            borderWidth: 1,
            fill: false,
            tension: 0.4,
            pointRadius: 1,
            pointHoverRadius: 4,
            borderDash: [4, 4]
          },
          {
            label: `ROI`,
            data: roi,
            borderColor: '#eab308',
            backgroundColor: 'rgba(234, 179, 8, 0.1)',
            borderWidth: 1.5,
            yAxisID: 'y1',
            fill: false,
            tension: 0.4,
            pointRadius: 1,
            pointHoverRadius: 4
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false,
        },
        plugins: {
          legend: {
            display: true,
            position: 'top',
            labels: {
              usePointStyle: true,
              padding: 10,
              color: '#9ca3af',
              font: { size: 11 }
            }
          },
          title: {
            display: true,
            text: `${this.data.instrument === 'nps' ? 'NPS' : 'Index Fund'} — Age ${this.data.startAge} → 60`,
            color: '#e8eaf0',
            font: { size: 12, weight: 'bold' },
            padding: { top: 10, bottom: 10 }
          },
          tooltip: {
            backgroundColor: 'rgba(19, 22, 30, 0.95)',
            borderColor: '#252836',
            borderWidth: 1,
            padding: 10,
            titleColor: '#e8eaf0',
            bodyColor: '#9ca3af',
            callbacks: {
              afterLabel: (context: any) => {
                const entry = timeline[context.dataIndex];
                return entry ? `Age: ${entry.age} | Growth: ${(entry.growthRate * 100).toFixed(1)}%` : '';
              }
            }
          }
        },
        scales: {
          y: {
            type: 'linear',
            display: true,
            position: 'left',
            grid: { color: 'rgba(37, 40, 54, 0.8)' },
            ticks: {
              color: '#9ca3af',
              callback: (value: any) => '₹' + (value as number).toLocaleString()
            }
          },
          y1: {
            type: 'linear',
            display: true,
            position: 'right',
            grid: { drawOnChartArea: false },
            ticks: {
              color: '#9ca3af',
              callback: (value: any) => '₹' + (value as number).toLocaleString()
            }
          },
          x: {
            grid: { color: 'rgba(37, 40, 54, 0.8)' },
            ticks: {
              color: '#9ca3af',
              maxRotation: 45
            }
          }
        }
      }
    } as ChartConfiguration<'line'>;
  }

  toggleMetric(metric: 'nominal' | 'real'): void {
    this.selectedMetric = metric;
    // Can add logic to update chart display
  }

  selectMilestone(milestone: MilestoneEntry): void {
    this.selectedMilestone = milestone;
  }

  get retirementEntry(): TimeSeriesEntry | undefined {
    return this.data?.timeline.find(t => t.isRetirement);
  }

  get maxValue(): number {
    if (!this.data?.timeline) return 0;
    return Math.max(...this.data.timeline.map(t => t.realValue));
  }

  get totalROI(): number {
    const retirement = this.retirementEntry;
    return retirement ? retirement.roi : 0;
  }
}
