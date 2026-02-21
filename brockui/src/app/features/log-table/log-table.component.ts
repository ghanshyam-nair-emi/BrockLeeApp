import { Component, Input } from '@angular/core';
import { CommonModule, DecimalPipe, DatePipe } from '@angular/common';
import { UserLog, LogMetrics } from '../../core/models';

@Component({
  selector:    'app-log-table',
  standalone:  true,
  imports:     [CommonModule, DecimalPipe, DatePipe],
  templateUrl: './log-table.component.html',
  styleUrls:   ['./log-table.component.scss']
})
export class LogTableComponent {
  @Input() logs:        UserLog[]    = [];
  @Input() metrics:     LogMetrics  | null = null;
  @Input() isRefreshing: boolean    = false;
}