import {
  Component, Input, Output, EventEmitter, OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { RiskProfileResponse } from '../../core/models';

@Component({
  selector:    'app-risk-profile',
  standalone:  true,
  imports:     [CommonModule],
  templateUrl: './risk-profile.component.html',
  styleUrls:   ['./risk-profile.component.scss']
})
export class RiskProfileComponent implements OnInit {

  @Input() data: RiskProfileResponse | null = null;
  @Input() isLoading: boolean = false;
  @Output() instrumentSelected = new EventEmitter<string>();

  riskBadgeClass = '';
  confidencePercentage = 0;
  selectedTab: 'overview' | 'advantages' = 'overview';

  ngOnInit(): void {
    if (this.data) {
      this.updateRiskBadge();
      this.confidencePercentage = Math.round(this.data.confidence * 100);
    }
  }

  ngOnChanges(): void {
    if (this.data) {
      this.updateRiskBadge();
      this.confidencePercentage = Math.round(this.data.confidence * 100);
    }
  }

  updateRiskBadge(): void {
    if (!this.data) return;
    
    const profile = this.data.riskProfile?.toLowerCase() || '';
    if (profile.includes('aggressive')) {
      this.riskBadgeClass = 'badge-aggressive';
    } else if (profile.includes('moderate')) {
      this.riskBadgeClass = 'badge-moderate';
    } else {
      this.riskBadgeClass = 'badge-conservative';
    }
  }

  selectInstrument(instrument: string): void {
    this.instrumentSelected.emit(instrument);
  }

  switchTab(tab: 'overview' | 'advantages'): void {
    this.selectedTab = tab;
  }

  get recommendedDisplay(): string {
    if (!this.data) return '';
    return this.data.recommendedInstrument === 'nps' ? 'NPS (National Pension Scheme)' : 'Index Fund (e.g., NIFTY 50)';
  }

  get volatilityLabel(): string {
    if (!this.data) return '';
    const vol = this.data.expenseVolatility;
    if (vol < 0.2) return 'Very Stable';
    if (vol < 0.4) return 'Stable';
    if (vol < 0.6) return 'Moderate';
    if (vol < 0.8) return 'Volatile';
    return 'Highly Volatile';
  }

  get stabilityLabel(): string {
    if (!this.data) return '';
    const score = this.data.stabilityScore;
    if (score > 0.75) return 'Very Stable';
    if (score > 0.5) return 'Moderately Stable';
    return 'Less Stable';
  }

  getVolatilityColor(): string {
    const vol = this.data?.expenseVolatility || 0;
    if (vol < 0.2) return '#28a745';
    if (vol < 0.4) return '#17a2b8';
    if (vol < 0.6) return '#ffc107';
    if (vol < 0.8) return '#fd7e14';
    return '#dc3545';
  }

  getStabilityColor(): string {
    const score = this.data?.stabilityScore || 0;
    if (score > 0.75) return '#28a745';
    if (score > 0.5) return '#17a2b8';
    return '#dc3545';
  }
}
