import {
  Component, EventEmitter, Output, OnInit
} from '@angular/core';
import {
  FormBuilder, FormGroup,
  Validators, ReactiveFormsModule,
  FormsModule                        // ← FIX: required for [(ngModel)] on textareas
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import {
  Expense, Transaction, QPeriod,
  PPeriod, KPeriod, ReturnsRequest
} from '../../core/models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector:    'app-user-form',
  standalone:  true,
  imports:     [
    CommonModule,
    ReactiveFormsModule,
    FormsModule            // ← FIX: standalone components must import this explicitly
  ],
  templateUrl: './user-form.component.html',
  styleUrls:   ['./user-form.component.scss']
})
export class UserFormComponent implements OnInit {

  @Output() submitted = new EventEmitter<ReturnsRequest>();
  @Output() loading   = new EventEmitter<boolean>();

  form!: FormGroup;
  isLoading = false;
  errorMsg  = '';

  expensesJson = JSON.stringify([
    { date: '2023-10-12T20:15:00', amount: 250 },
    { date: '2023-02-28T15:49:00', amount: 375 },
    { date: '2023-07-01T21:59:00', amount: 620 },
    { date: '2023-12-17T08:09:00', amount: 480 }
  ], null, 2);

  qJson = JSON.stringify([
    { fixed: 0, start: '2023-07-01T00:00:00', end: '2023-07-31T23:59:59' }
  ], null, 2);

  pJson = JSON.stringify([
    { extra: 25, start: '2023-10-01T08:00:00', end: '2023-12-31T19:59:00' }
  ], null, 2);

  kJson = JSON.stringify([
    { start: '2023-03-01T00:00:00', end: '2023-11-30T23:59:59' },
    { start: '2023-01-01T00:00:00', end: '2023-12-31T23:59:59' }
  ], null, 2);

  jsonErrors: Record<string, string> = {};

  constructor(
    private fb:  FormBuilder,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      name:      ['', [Validators.required, Validators.minLength(2)]],
      age:       [29,    [Validators.required, Validators.min(1), Validators.max(59)]],
      wage:      [50000, [Validators.required, Validators.min(1)]],
      inflation: [0.055, [Validators.required, Validators.min(0), Validators.max(1)]]
    });
  }

  get nameCtrl()      { return this.form.get('name')!; }
  get ageCtrl()       { return this.form.get('age')!; }
  get wageCtrl()      { return this.form.get('wage')!; }
  get inflationCtrl() { return this.form.get('inflation')!; }

  parseJson<T>(key: string, value: string): T | null {
    try {
      delete this.jsonErrors[key];
      return JSON.parse(value) as T;
    } catch {
      this.jsonErrors[key] = `Invalid JSON in ${key}`;
      return null;
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.jsonErrors = {};

    const expenses = this.parseJson<Expense[]>('expenses', this.expensesJson);
    const q        = this.parseJson<QPeriod[]>('q', this.qJson);
    const p        = this.parseJson<PPeriod[]>('p', this.pJson);
    const k        = this.parseJson<KPeriod[]>('k', this.kJson);

    if (!expenses || q === null || p === null || k === null) return;

    this.isLoading = true;
    this.errorMsg  = '';
    this.loading.emit(true);

    try {
      // Step 1: Parse expenses → transactions
      const parsed = await this.api.parse({ expenses }).toPromise();
      if (!parsed) throw new Error('Parse returned empty.');

      // Step 2: Validate transactions
      const validated = await this.api.validate({
        wage:         this.form.value.wage,
        transactions: parsed.transactions
      }).toPromise();
      if (!validated) throw new Error('Validation returned empty.');

      if (validated.valid.length === 0) {
        this.errorMsg = 'All transactions failed validation. Check your input.';
        return;
      }

      // Step 3: Emit full request to parent dashboard
      const request: ReturnsRequest = {
        name:         this.form.value.name,
        age:          this.form.value.age,
        wage:         this.form.value.wage,
        inflation:    this.form.value.inflation,
        transactions: validated.valid,
        q, p, k
      };

      this.submitted.emit(request);

    } catch (err: any) {
      this.errorMsg = err?.error?.message ?? err?.message ?? 'An error occurred.';
    } finally {
      this.isLoading = false;
      this.loading.emit(false);
    }
  }
}