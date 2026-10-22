import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { API_BASE_URL } from '../core/api/api-config';
import { COUNTRIES, CURRENCIES } from '../core/locale/iso-lists';
import { applyProblemDetailsToForm, asProblem, controlError } from '../core/http/problem-details';
import { AUTH_STYLES, PASSWORD_HINT, PASSWORD_RULE } from './auth-layout';

interface IndustryView { key: string; label: string; description: string; availability: string; suggestedModules: string[]; installedModules: string[]; }

/** Self sign-up: creates a workspace from an industry template. Country and base currency are chosen here and fixed afterwards. */
@Component({
  selector: 'erp-onboarding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzSelectModule, NzAlertModule],
  styles: [AUTH_STYLES],
  template: `
    <div class="card wide">
      <div class="brand"><span class="brand-mark">E</span> ERP</div>
      <h1>Set up your workspace</h1>
      <p class="sub">Tell us about your business. Country and base currency cannot be changed after setup.</p>
      @if (error()) { <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon style="margin-bottom:16px" data-testid="onboarding-error"></nz-alert> }
      <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
        <nz-form-item><nz-form-label nzRequired>Workspace name</nz-form-label>
          <nz-form-control [nzErrorTip]="err('workspaceName')"><input nz-input formControlName="workspaceName" data-testid="ob-name" /></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Workspace ID</nz-form-label>
          <nz-form-control [nzErrorTip]="err('signInKey')" nzExtra="Lower-case letters, digits and dashes."><input nz-input formControlName="signInKey" data-testid="ob-key" /></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Industry</nz-form-label>
          <nz-form-control [nzErrorTip]="err('industry')"><nz-select formControlName="industry" data-testid="ob-industry">
            @for (i of industries(); track i.key) { <nz-option [nzValue]="i.key" [nzLabel]="i.label"></nz-option> }
          </nz-select></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Country</nz-form-label>
          <nz-form-control [nzErrorTip]="err('countryCode')"><nz-select formControlName="countryCode" nzShowSearch data-testid="ob-country">
            @for (c of countries; track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.name"></nz-option> }
          </nz-select></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Base currency</nz-form-label>
          <nz-form-control [nzErrorTip]="err('baseCurrency')"><nz-select formControlName="baseCurrency" nzShowSearch data-testid="ob-currency">
            @for (c of currencies; track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.code + ' · ' + c.name"></nz-option> }
          </nz-select></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label>Legal entity name</nz-form-label>
          <nz-form-control><input nz-input formControlName="legalEntityName" placeholder="Defaults to the workspace name" /></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Your name</nz-form-label>
          <nz-form-control [nzErrorTip]="err('adminName')"><input nz-input formControlName="adminName" data-testid="ob-admin-name" /></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Email</nz-form-label>
          <nz-form-control [nzErrorTip]="err('adminEmail')"><input nz-input type="email" formControlName="adminEmail" autocomplete="username" data-testid="ob-email" /></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Password</nz-form-label>
          <nz-form-control [nzErrorTip]="err('adminPassword')" [nzExtra]="passwordHint"><input nz-input type="password" formControlName="adminPassword" autocomplete="new-password" data-testid="ob-password" /></nz-form-control></nz-form-item>
        <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" data-testid="ob-submit">Create workspace</button>
      </form>
      <p style="margin-top:16px">Already have a workspace? <a routerLink="/auth/login">Sign in</a></p>
    </div>`,
})
export class OnboardingPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);

  readonly countries = COUNTRIES;
  readonly currencies = CURRENCIES;
  readonly passwordHint = PASSWORD_HINT;
  readonly industries = signal<IndustryView[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly form = inject(FormBuilder).nonNullable.group({
    workspaceName: ['', [Validators.required, Validators.maxLength(100)]],
    signInKey: ['', [Validators.required, Validators.pattern(/^[a-z0-9][a-z0-9-]{1,48}$/)]],
    industry: ['', Validators.required],
    countryCode: ['', Validators.required],
    baseCurrency: ['', Validators.required],
    legalEntityName: [''],
    adminName: ['', Validators.required],
    adminEmail: ['', [Validators.required, Validators.email]],
    adminPassword: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
  });

  constructor() {
    this.form.controls.countryCode.valueChanges.subscribe((code) => this.onCountry(code));
    this.http.get<IndustryView[]>(`${this.base}/platform/industries`).subscribe({
      next: (i) => this.industries.set(i.filter((x) => x.availability === 'Available')),
      error: () => this.error.set('Could not load the industry list. Refresh to try again.'),
    });
  }

  err(name: string): string { return controlError(this.form.get(name)); }

  /** Suggest the country's usual currency; the user can still pick any other base currency. */
  onCountry(code: string): void {
    const c = COUNTRIES.find((x) => x.code === code);
    if (c && !this.form.controls.baseCurrency.dirty) this.form.controls.baseCurrency.setValue(c.currency);
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const country = COUNTRIES.find((c) => c.code === v.countryCode);
    this.busy.set(true);
    this.error.set(null);
    this.http.post(`${this.base}/platform/onboarding`, {
      ...v, legalEntityName: v.legalEntityName || null, timeZone: country?.timeZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone,
    }).subscribe({
      next: () => { this.busy.set(false); void this.router.navigateByUrl('/auth/login'); },
      error: (e) => {
        this.busy.set(false);
        const p = asProblem(e);
        const unmatched = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.error.set(unmatched[0] ?? (p?.status === 403 ? 'Self sign-up is disabled for this installation.' : (p?.detail ?? p?.title ?? 'Could not create the workspace.')));
      },
    });
  }
}
