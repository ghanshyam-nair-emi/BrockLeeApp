import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import {
  PredictResponse, ModelPrediction,
  FeatureImportance, PredictState
} from '../../core/models';

@Component({
  selector:    'app-prediction-panel',
  standalone:  true,
  imports:     [CommonModule, DecimalPipe],
  templateUrl: './prediction-panel.component.html',
  styleUrls:   ['./prediction-panel.component.scss']
})
export class PredictionPanelComponent implements OnChanges {

  @Input() prediction:   PredictResponse | null = null;
  @Input() predictState: PredictState = 'idle';

  activeMetric: 'nps' | 'index' = 'nps';

  // ── Safe getters — all return empty/zero when data missing ────────────────

  get models(): ModelPrediction[] {
    return this.prediction?.models ?? [];
  }

  get sortedFeatures(): FeatureImportance[] {
    return [...(this.prediction?.featureImportance ?? [])]
      .sort((a, b) => b.importance - a.importance);
  }

  get maxImportance(): number {
    return this.sortedFeatures[0]?.importance ?? 1;
  }

  // ── Template helpers ──────────────────────────────────────────────────────

  agreementClass(level: string | undefined): string {
    if (level === 'HIGH')     return 'badge-high';
    if (level === 'MODERATE') return 'badge-moderate';
    return 'badge-low';
  }

  confidenceClass(conf: number): string {
    if (conf >= 90) return 'conf-high';
    if (conf >= 70) return 'conf-med';
    return 'conf-low';
  }

  // FIX: guard against undefined nps/index sub-objects on each model
  barWidth(model: ModelPrediction): number {
    if (!model?.nps || !model?.index) return 0;

    const vals = this.models
      .filter(m => m?.nps && m?.index)
      .map(m =>
        this.activeMetric === 'nps'
          ? (m.nps?.predictedValue ?? 0)
          : (m.index?.predictedValue ?? 0)
      );

    const max = Math.max(...vals, 1);
    const val = this.activeMetric === 'nps'
      ? (model.nps?.predictedValue ?? 0)
      : (model.index?.predictedValue ?? 0);

    return (val / max) * 100;
  }

  predictedValue(model: ModelPrediction): number {
    if (!model?.nps || !model?.index) return 0;
    return this.activeMetric === 'nps'
      ? (model.nps?.predictedValue  ?? 0)
      : (model.index?.predictedValue ?? 0);
  }

  confidence(model: ModelPrediction): number {
    if (!model?.nps || !model?.index) return 0;
    return this.activeMetric === 'nps'
      ? (model.nps?.confidence  ?? 0)
      : (model.index?.confidence ?? 0);
  }

  r2(model: ModelPrediction): number {
    if (!model?.nps || !model?.index) return 0;
    return this.activeMetric === 'nps'
      ? (model.nps?.r2Score  ?? 0)
      : (model.index?.r2Score ?? 0);
  }

  isBestModel(model: ModelPrediction): boolean {
    // FIX: guard consensus before accessing bestModel
    return !!this.prediction?.consensus &&
           model.modelName === this.prediction.consensus.bestModel;
  }

  setMetric(m: 'nps' | 'index'): void { this.activeMetric = m; }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['prediction'] && this.prediction) {
      this.activeMetric = 'nps';
    }
  }
}