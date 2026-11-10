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
import { CountryDto, CurrencyOption } from '@core/api/admin.api';
import { BillingCycle, IndustryOption, PlatformApi, groupIndustries, PublicPlan, SignupConfig, SignupResult } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { suggestWorkspaceCode, yearlySaving } from '@core/format/subscription';
import { applyProblemDetailsToForm, asProblem, controlError } from '@core/http/problem-details';
import { BrandAssistantComponent } from '@shared/components/brand-assistant/brand-assistant.component';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { PlanCardComponent } from '@shared/components/plan-card/plan-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { PASSWORD_HINT, PASSWORD_RULE } from '../../auth-layout';
import { RegisterResultComponent } from '../../components/register-result/register-result.component';

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
  styleUrl: './register.page.scss',
  templateUrl: './register.page.html',
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
