import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { catchError, debounceTime, of, switchMap, tap } from 'rxjs';
import { CountryDto, CurrencyOption } from '../core/api/admin.api';
import { BillingCycle, IndustryOption, PlatformApi, groupIndustries, PublicPlan, SignupConfig, SignupResult } from '../core/api/platform.api';
import { displayMoney } from '../core/format/display';
import { suggestWorkspaceCode, yearlySaving } from '../core/format/subscription';
import { applyProblemDetailsToForm, asProblem, controlError } from '../core/http/problem-details';
import { BrandAssistantComponent } from '../shared/brand-assistant.component';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { PlanCardComponent } from '../shared/plan-card.component';
import { StatePanelComponent } from '../shared/state-panel.component';
import { PASSWORD_HINT, PASSWORD_RULE } from './auth-layout';
import { RegisterResultComponent } from './register-result.component';

const CODE_RULE = /^[a-zA-Z0-9][a-zA-Z0-9-]{1,48}$/;
const FIELD_MESSAGES: Record<string, string> = {
  businessName: 'Enter the business name.', signInKey: 'Use 2-49 letters, digits or dashes.', industry: 'Choose an industry.', countryCode: 'Choose a country.', baseCurrency: 'Choose a currency.',
  contactName: 'Enter your name.', email: 'Enter a valid email address.', password: PASSWORD_HINT, acceptTerms: 'Please accept the terms to continue.',
};

/** The public way in: a business picks a plan and registers. What happens next depends on the founder's registration mode. */
@Component({
  selector: 'erp-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrandAssistantComponent, ReactiveFormsModule, FormsModule, RouterLink, NzAlertModule, NzButtonModule, NzCheckboxModule, NzFormModule, NzIconModule, NzInputModule, NzRadioModule, NzSelectModule, NzSpinModule,
    BrandLogoComponent, PlanCardComponent, RegisterResultComponent, StatePanelComponent],
  styles: [`
    :host { display: block; min-height: 100vh; background: radial-gradient(circle at 10% 20%, rgba(16,149,93,.06) 0%, transparent 40%), var(--erp-color-bg-canvas); }
    .top { display: flex; align-items: center; justify-content: space-between; gap: 12px; max-width: 1120px; margin: 0 auto; padding: 18px 24px; }
    .top erp-brand-logo { width: auto; }
    .top a.signin { font-weight: 600; white-space: nowrap; }
    main { max-width: 1120px; margin: 0 auto; padding: 8px 24px 56px; }
    .intro { text-align: center; margin: 8px auto 24px; max-width: 640px; }
    .intro h1 { font-size: 30px; font-weight: 800; letter-spacing: -.02em; margin: 0 0 8px; color: var(--erp-color-text-primary); }
    .intro p { margin: 0; color: var(--erp-color-text-muted); font-size: 15px; line-height: 1.5; }
    .cycle { display: flex; flex-direction: column; align-items: center; gap: 6px; margin-bottom: 28px; }
    .cycle small { color: var(--erp-color-success-text); font-weight: 600; }
    .plans { display: grid; gap: 20px; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); align-items: stretch; }
    .note { text-align: center; margin: 20px 0 0; color: var(--erp-color-text-muted); font-size: 13px; }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius: 16px; padding: 28px; max-width: 680px; margin: 0 auto; min-width: 0; }
    .card h1 { font-size: 22px; font-weight: 700; margin: 0 0 4px; color: var(--erp-color-text-primary); }
    .card > p.sub { margin: 0 0 18px; color: var(--erp-color-text-muted); }
    .chosen { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px 12px; padding: 10px 14px; margin-bottom: 20px; border-radius: 10px; background: var(--erp-color-primary-subtle); border: 1px solid var(--erp-color-primary-border); }
    .chosen strong { color: var(--erp-color-text-primary); }
    .chosen span { color: var(--erp-color-text-secondary); font-size: 13px; margin-inline-start: 6px; }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); column-gap: 16px; }
    .grid .wide { grid-column: 1 / -1; }
    .code-state { font-size: 12.5px; font-weight: 600; }
    .code-state.free { color: var(--erp-color-success-text); }
    .code-state.taken { color: var(--erp-color-error-text); }
    .code-state.checking { color: var(--erp-color-text-muted); font-weight: 400; }
    .terms { display: flex; flex-wrap: wrap; align-items: baseline; gap: 0 4px; }
    .terms label { margin-inline-end: 0; }
    .hp { position: absolute; left: -10000px; top: auto; width: 1px; height: 1px; overflow: hidden; }
    .field-hint { color: var(--erp-color-text-muted); font-size: 12.5px; }
    @media (max-width: 767px) {
      .top { padding: 14px 16px; }
      main { padding: 4px 16px 40px; }
      .intro h1 { font-size: 24px; }
      .card { padding: 20px 16px; border-radius: 12px; }
      .grid { grid-template-columns: minmax(0, 1fr); }
    }
  `],
  template: `
    <header class="top">
      <a routerLink="/pricing" aria-label="Home" style="display:inline-flex;min-width:0"><erp-brand-logo size="lg"></erp-brand-logo></a>
      <a class="signin" routerLink="/auth/login" data-testid="register-signin-link">Sign in</a>
    </header>
    <main>
      @if (loading()) {
        <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
      } @else if (loadFailed()) {
        <erp-state-panel kind="error" title="This page could not load" hint="The server did not answer. Please try again in a moment." actionLabel="Try again" (action)="load()" />
      } @else if (config(); as c) {
        @if (result(); as r) {
          <div class="card"><erp-register-result [result]="r" [supportEmail]="c.supportEmail" /></div>
        } @else if (c.mode === 'closed') {
          <erp-state-panel icon="lock" title="Registration is closed right now"
            [hint]="c.supportEmail ? 'To get started, write to ' + c.supportEmail + ' and we will set you up.' : 'Please check back soon.'" data-testid="register-closed" />
        } @else if (step() === 'plans') {
          <div class="intro">
            <h1>Choose a plan for your business</h1>
            <p>Pick what fits today. You can change your plan at any time.</p>
          </div>
          @if (c.plans.length === 0) {
            <erp-state-panel title="No plans are on offer right now" [hint]="c.supportEmail ? 'Please write to ' + c.supportEmail + '.' : 'Please check back soon.'" data-testid="register-no-plans" />
          } @else {
            <div class="cycle">
              <nz-radio-group [ngModel]="cycle()" (ngModelChange)="cycle.set($event)" nzButtonStyle="solid" data-testid="register-cycle">
                <label nz-radio-button nzValue="monthly" data-testid="register-cycle-monthly">Monthly</label>
                <label nz-radio-button nzValue="annual" data-testid="register-cycle-annual">Annual</label>
              </nz-radio-group>
              @if (bestSaving(); as s) { <small>Pay annually and save up to {{ s }}%</small> }
            </div>
            <div class="plans">
              @for (p of c.plans; track p.id) {
                <erp-plan-card [plan]="p" [cycle]="cycle()" [actionLabel]="'Choose ' + p.name" (choose)="pick($event)" />
              }
            </div>
            <p class="note">No card needed. When you continue after the trial, we send an invoice that you pay by bank transfer.</p>
          }
        } @else {
          <div class="card">
            <h1>Create your workspace</h1>
            <p class="sub">Tell us about your business. It takes about a minute.</p>
            @if (plan(); as p) {
              <div class="chosen" data-testid="register-chosen">
                <div><strong>{{ p.name }}</strong> <span>{{ chosenPrice() }}{{ p.trialDays > 0 ? ', ' + p.trialDays + ' days free trial' : '' }}</span></div>
                <button nz-button nzType="link" nzSize="small" type="button" (click)="step.set('plans')" data-testid="register-change-plan">Change plan</button>
              </div>
            }
            @if (error()) { <nz-alert nzType="error" nzShowIcon [nzMessage]="error()!" style="margin-bottom:16px" data-testid="register-error"></nz-alert> }
            <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()" class="grid" novalidate>
              <nz-form-item class="wide">
                <nz-form-label nzRequired nzFor="reg-business">Business name</nz-form-label>
                <nz-form-control [nzErrorTip]="err('businessName')">
                  <input nz-input id="reg-business" formControlName="businessName" autocomplete="organization" data-testid="register-business" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item class="wide">
                <nz-form-label nzRequired nzFor="reg-code">Workspace code</nz-form-label>
                <nz-form-control [nzErrorTip]="err('signInKey')" [nzExtra]="codeExtra">
                  <input nz-input id="reg-code" formControlName="signInKey" autocomplete="off" spellcheck="false" (input)="codeEdited.set(!!$any($event.target).value)" data-testid="register-code" />
                </nz-form-control>
                <ng-template #codeExtra>
                  @switch (codeState()) {
                    @case ('checking') { <span class="code-state checking">Checking...</span> }
                    @case ('free') { <span class="code-state free" data-testid="register-code-free"><span nz-icon nzType="check-circle"></span> This code is free</span> }
                    @case ('taken') { <span class="code-state taken" data-testid="register-code-taken">{{ codeReason() }}</span> }
                    @default { <span class="field-hint">A short name for your workspace. You can use letters, digits and dashes.</span> }
                  }
                </ng-template>
              </nz-form-item>
              <nz-form-item class="wide">
                <nz-form-label nzRequired nzFor="reg-industry">Industry</nz-form-label>
                <nz-form-control [nzErrorTip]="err('industry')">
                  <nz-select nzId="reg-industry" formControlName="industry" nzShowSearch nzPlaceHolder="Choose an industry" data-testid="register-industry">
                    @for (g of groupedIndustries(); track g.group) {
                      <nz-option-group [nzLabel]="g.group">
                        @for (i of g.items; track i.key) {
                          <nz-option [nzValue]="i.key" [nzLabel]="i.label"></nz-option>
                        }
                      </nz-option-group>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired nzFor="reg-country">Country</nz-form-label>
                <nz-form-control [nzErrorTip]="err('countryCode')">
                  <nz-select nzId="reg-country" formControlName="countryCode" nzShowSearch nzPlaceHolder="Choose a country" data-testid="register-country">
                    @for (c of countries(); track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.name"></nz-option> }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired nzFor="reg-currency">Currency</nz-form-label>
                <nz-form-control [nzErrorTip]="err('baseCurrency')">
                  <nz-select nzId="reg-currency" formControlName="baseCurrency" nzShowSearch nzPlaceHolder="Choose a currency" data-testid="register-currency">
                    @for (c of currencies(); track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.code + ' · ' + c.name"></nz-option> }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzRequired nzFor="reg-name">Your name</nz-form-label>
                <nz-form-control [nzErrorTip]="err('contactName')">
                  <input nz-input id="reg-name" formControlName="contactName" autocomplete="name" data-testid="register-name" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <nz-form-label nzFor="reg-phone">Phone (optional)</nz-form-label>
                <nz-form-control [nzErrorTip]="err('phone')">
                  <input nz-input id="reg-phone" type="tel" formControlName="phone" autocomplete="tel" data-testid="register-phone" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item class="wide">
                <nz-form-label nzRequired nzFor="reg-email">Work email</nz-form-label>
                <nz-form-control [nzErrorTip]="err('email')">
                  <input nz-input id="reg-email" type="email" formControlName="email" autocomplete="email" data-testid="register-email" />
                </nz-form-control>
              </nz-form-item>
              <nz-form-item class="wide">
                <nz-form-label nzRequired nzFor="reg-password">Password</nz-form-label>
                <nz-form-control [nzErrorTip]="err('password')" [nzExtra]="hint + ' Already have an account with this email? Enter its password and the new workspace joins your account.'">
                  <input nz-input id="reg-password" type="password" formControlName="password" autocomplete="new-password" data-testid="register-password" />
                </nz-form-control>
              </nz-form-item>
              <div class="hp" aria-hidden="true">
                <label for="reg-website">Website</label>
                <input id="reg-website" type="text" formControlName="website" tabindex="-1" autocomplete="off" data-testid="register-website" />
              </div>
              <nz-form-item class="wide">
                <nz-form-control [nzErrorTip]="err('acceptTerms')">
                  <!-- The links sit outside the label: the checkbox would otherwise swallow their clicks. They open a new tab, so nothing typed here is lost. -->
                  <div class="terms">
                    <label nz-checkbox formControlName="acceptTerms" data-testid="register-terms">I accept the</label>
                    @if (c.termsUrl) {
                      <a [href]="c.termsUrl" target="_blank" rel="noopener" data-testid="register-terms-link">Terms of Service</a>
                    } @else {
                      <a routerLink="/terms" target="_blank" data-testid="register-terms-link">Terms of Service</a>
                    }
                    <span>and</span>
                    <a routerLink="/privacy" target="_blank" data-testid="register-privacy-link">Privacy Policy</a>
                  </div>
                </nz-form-control>
              </nz-form-item>
              <div class="wide">
                <button nz-button nzType="primary" nzBlock nzSize="large" [nzLoading]="busy()" data-testid="register-submit">Create workspace</button>
              </div>
            </form>
          </div>
        }
      }
    </main>
    @defer (on idle) { <erp-brand-assistant /> }`,
})
export class RegisterPage {
  private readonly api = inject(PlatformApi);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly hint = PASSWORD_HINT;
  readonly config = signal<SignupConfig | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly step = signal<'plans' | 'form'>('plans');
  readonly cycle = signal<BillingCycle>('monthly');
  readonly plan = signal<PublicPlan | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<SignupResult | null>(null);
  readonly codeEdited = signal(false);
  readonly codeState = signal<'idle' | 'checking' | 'free' | 'taken'>('idle');
  readonly codeReason = signal<string | null>(null);

  readonly industries = toSignal(this.api.industries().pipe(catchError(() => of([] as IndustryOption[]))), { initialValue: [] as IndustryOption[] });
  readonly countries = toSignal(this.api.countries().pipe(catchError(() => of([] as CountryDto[]))), { initialValue: [] as CountryDto[] });
  readonly currencies = toSignal(this.api.currencies().pipe(catchError(() => of([] as CurrencyOption[]))), { initialValue: [] as CurrencyOption[] });

  readonly form = inject(FormBuilder).nonNullable.group({
    businessName: ['', [Validators.required, Validators.maxLength(200)]],
    signInKey: ['', [Validators.required, Validators.pattern(CODE_RULE)]],
    industry: ['', Validators.required],
    countryCode: ['', Validators.required],
    baseCurrency: ['', Validators.required],
    contactName: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', Validators.maxLength(40)],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
    acceptTerms: [false, Validators.requiredTrue],
    website: [''],
  });

  readonly bestSaving = computed(() => {
    const percents = (this.config()?.plans ?? []).map((p) => yearlySaving(p.monthlyPrice, p.annualPrice)?.percent ?? 0);
    const best = Math.max(0, ...percents);
    return best > 0 ? best : null;
  });
  readonly chosenPrice = computed(() => {
    const p = this.plan();
    if (!p) return '';
    return this.cycle() === 'annual' ? `${displayMoney(p.annualPrice, p.currency)} a year` : `${displayMoney(p.monthlyPrice, p.currency)} a month`;
  });

  /** time zone suggested by the chosen country; falls back to the browser's */
  private suggestedZone: string | null = null;
  private suggestedCurrency: string | null = null;

  constructor() {
    const controls = this.form.controls;
    controls.businessName.valueChanges.pipe(takeUntilDestroyed()).subscribe((name) => {
      if (!this.codeEdited()) controls.signInKey.setValue(suggestWorkspaceCode(name));
    });
    controls.countryCode.valueChanges.pipe(takeUntilDestroyed()).subscribe((code) => {
      const c = this.countries().find((x) => x.code === code);
      if (!c) return;
      this.suggestedZone = c.defaultTimeZone;
      // Follows the country until the person picks a different currency.
      const current = controls.baseCurrency.value;
      if (!current || current === this.suggestedCurrency) controls.baseCurrency.setValue(c.defaultCurrency);
      this.suggestedCurrency = c.defaultCurrency;
    });
    controls.signInKey.valueChanges.pipe(
      tap((key) => { this.codeState.set(CODE_RULE.test(key.trim()) ? 'checking' : 'idle'); this.codeReason.set(null); }),
      debounceTime(400),
      // A failed check (a rate limit, a dropped connection) says nothing: the server checks again on submit.
      switchMap((key) => (CODE_RULE.test(key.trim()) ? this.api.signInKeyAvailable(key.trim()).pipe(catchError(() => of(null))) : of(null))),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((r) => {
      if (!r) { this.codeState.set('idle'); return; }
      this.codeState.set(r.available ? 'free' : 'taken');
      this.codeReason.set(r.reason);
    });
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.api.signupConfig().subscribe({
      next: (c) => {
        this.config.set(c);
        this.loading.set(false);
        const code = this.route.snapshot.queryParamMap.get('plan');
        const wanted = code ? c.plans.find((p) => p.code === code.toLowerCase()) : null;
        if (this.route.snapshot.queryParamMap.get('cycle') === 'annual') this.cycle.set('annual');
        if (wanted) this.pick(wanted);
      },
      error: () => { this.loading.set(false); this.loadFailed.set(true); },
    });
  }

  /** The business types a new workspace can start with, grouped by sector. */
  readonly groupedIndustries = computed(() => groupIndustries(this.industries()));
  err(name: string): string {
    const control = this.form.get(name);
    return control?.errors?.['server'] ?? FIELD_MESSAGES[name] ?? controlError(control);
  }

  pick(plan: PublicPlan): void {
    this.plan.set(plan);
    this.step.set('form');
    if (typeof window !== 'undefined') window.scrollTo?.({ top: 0 });
  }

  submit(): void {
    const form = this.form;
    if (form.invalid) {
      Object.values(form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); });
      return;
    }
    const v = form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.api.register({
      businessName: v.businessName.trim(), signInKey: v.signInKey.trim(), industry: v.industry, countryCode: v.countryCode, baseCurrency: v.baseCurrency,
      timeZone: this.suggestedZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone, contactName: v.contactName.trim(), email: v.email.trim(), phone: v.phone.trim() || null, password: v.password,
      planId: this.plan()?.id ?? null, cycle: this.cycle(), acceptTerms: v.acceptTerms, website: v.website || null,
    }).subscribe({
      next: (r) => { this.busy.set(false); this.form.controls.password.reset(''); this.result.set(r); },
      error: (e) => {
        this.busy.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(form, p) : [];
        if (p?.status === 429) this.error.set('Too many attempts. Please wait a minute and try again.');
        else if (p?.errors && !rest.length) this.error.set(null);
        else this.error.set(rest[0] ?? p?.detail ?? p?.title ?? 'Could not create the workspace. Please try again.');
        if (form.controls.signInKey.errors?.['server']) this.codeState.set('idle');
      },
    });
  }
}
