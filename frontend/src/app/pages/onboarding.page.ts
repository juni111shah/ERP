import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { API_BASE_URL } from '../core/api/api-config';
import { CountryDto, CurrencyOption } from '../core/api/admin.api';
import { applyProblemDetailsToForm, asProblem } from '../core/http/problem-details';
import { AUTH_STYLES, PASSWORD_HINT, PASSWORD_RULE } from './auth-layout';

interface IndustryView { key: string; label: string; description: string; availability: string; suggestedModules: string[]; }

/** Create a workspace: pick an industry (template), country and base currency, and the first admin. */
@Component({
  selector: 'erp-onboarding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzRadioModule, NzSelectModule, NzAlertModule],
  styles: [AUTH_STYLES, `.industry { display: block; margin-bottom: 8px; } .industry small { display:block; color: var(--erp-color-text-secondary); margin-inline-start: 24px; }`],
  template: `
    <div class="card wide">
      <div class="brand"><span class="brand-mark">E</span> ERP</div>
      <h1>Create your workspace</h1>
      <p class="sub">Choose what your business does. You can enable more modules later.</p>
      @if (done()) {
        <nz-alert nzType="success" nzShowIcon nzMessage="Your workspace is ready." data-testid="onboarding-done"></nz-alert>
        <p style="margin-top:16px"><a routerLink="/auth/login">Sign in</a></p>
      } @else {
        @if (error()) { <nz-alert nzType="error" nzShowIcon [nzMessage]="error()!" style="margin-bottom:16px"></nz-alert> }
        <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
          <nz-form-item>
            <nz-form-label nzRequired>Industry</nz-form-label>
            <nz-form-control nzErrorTip="Choose an industry">
              <nz-radio-group formControlName="industry" data-testid="onboarding-industry">
                @for (i of industries(); track i.key) {
                  <label nz-radio class="industry" [nzValue]="i.key" [nzDisabled]="$any(i.availability)?.toLowerCase() !== 'available'" [attr.data-testid]="'industry-' + i.key">
                    {{ i.label }} @if ($any(i.availability)?.toLowerCase() !== 'available') { (coming soon) }
                    <small>{{ i.description }}</small>
                  </label>
                }
              </nz-radio-group>
            </nz-form-control>
          </nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Workspace name</nz-form-label><nz-form-control nzErrorTip="Required"><input nz-input formControlName="workspaceName" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Workspace code</nz-form-label><nz-form-control nzErrorTip="3–32 lowercase letters, digits or dashes"><input nz-input formControlName="signInKey" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Country</nz-form-label><nz-form-control nzErrorTip="Choose a country">
            <nz-select formControlName="countryCode" nzShowSearch nzPlaceHolder="Choose a country" (ngModelChange)="countryChanged($event)" data-testid="onboarding-country">
              @for (c of countries(); track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.name + (c.hasPack ? ' ✓' : '')"></nz-option> }
            </nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Base currency</nz-form-label><nz-form-control nzErrorTip="Choose a currency">
            <nz-select formControlName="baseCurrency" nzShowSearch nzPlaceHolder="Choose a currency" data-testid="onboarding-currency">
              @for (c of currencies(); track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.code + ' · ' + c.name"></nz-option> }
            </nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Your name</nz-form-label><nz-form-control nzErrorTip="Required"><input nz-input formControlName="adminName" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Work email</nz-form-label><nz-form-control nzErrorTip="Enter a valid email"><input nz-input type="email" formControlName="adminEmail" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Password</nz-form-label><nz-form-control [nzErrorTip]="hint"><input nz-input type="password" formControlName="adminPassword" autocomplete="new-password" /></nz-form-control></nz-form-item>
          <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" data-testid="onboarding-submit">Create workspace</button>
        </form>
      }
    </div>`,
})
export class OnboardingPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  readonly hint = PASSWORD_HINT;
  readonly industries = toSignal(this.http.get<IndustryView[]>(`${this.base}/platform/industries`).pipe(catchError(() => of([]))), { initialValue: [] });
  readonly countries = toSignal(this.http.get<CountryDto[]>(`${this.base}/platform/countries`).pipe(catchError(() => of([]))), { initialValue: [] as CountryDto[] });
  readonly currencies = toSignal(this.http.get<CurrencyOption[]>(`${this.base}/platform/currencies`).pipe(catchError(() => of([]))), { initialValue: [] as CurrencyOption[] });
  /** time zone suggested by the chosen country; falls back to the browser's */
  private suggestedZone: string | null = null;
  readonly form = inject(FormBuilder).nonNullable.group({
    industry: ['', Validators.required],
    workspaceName: ['', Validators.required],
    signInKey: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]{3,32}$/)]],
    countryCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]],
    baseCurrency: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    adminName: ['', Validators.required],
    adminEmail: ['', [Validators.required, Validators.email]],
    adminPassword: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
  });
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  /** Picking a country suggests its currency and time zone (still editable / overridable). */
  countryChanged(code: string): void {
    const c = this.countries().find((x) => x.code === code);
    if (!c) return;
    this.suggestedZone = c.defaultTimeZone;
    if (!this.form.controls.baseCurrency.dirty) this.form.controls.baseCurrency.setValue(c.defaultCurrency);
  }

  submit(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); });
      return;
    }
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.http.post(`${this.base}/platform/onboarding`, { ...v, countryCode: v.countryCode.toUpperCase(), baseCurrency: v.baseCurrency.toUpperCase(), timeZone: this.suggestedZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone })
      .subscribe({
        next: () => this.done.set(true),
        error: (e) => {
          this.busy.set(false);
          const p = asProblem(e);
          const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
          this.error.set(rest[0] ?? p?.detail ?? p?.title ?? 'Could not create the workspace.');
        },
      });
  }
}
