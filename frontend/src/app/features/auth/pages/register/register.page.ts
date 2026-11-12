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
import { BillingCycle, IndustryOption, PlatformApi, PublicPlan, SignupConfig, SignupResult, groupIndustries } from '@core/api/platform.api';
import { suggestWorkspaceCode } from '@core/format/subscription';
import { applyProblemDetailsToForm, asProblem, controlError } from '@core/http/problem-details';
import { BrandAssistantComponent } from '@shared/components/brand-assistant/brand-assistant.component';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { PlanCardComponent } from '@shared/components/plan-card/plan-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { revealErrors } from '@shared/forms/form-validation';
import { PASSWORD_HINT, PASSWORD_RULE } from '../../auth-layout';
import { CODE_RULE, FIELD_MESSAGES, bestYearlySaving, planPriceText, registrationFailureText, toSignupInput } from '../../auth-register';
import { RegisterResultComponent } from '../../components/register-result/register-result.component';

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

  readonly bestSaving = computed(() => bestYearlySaving(this.config()?.plans ?? []));
  readonly chosenPrice = computed(() => {
    const p = this.plan();
    if (!p) return '';
    return planPriceText(p, this.cycle());
  });
  /** The business types a new workspace can start with, grouped by sector. */
  readonly groupedIndustries = computed(() => groupIndustries(this.industries()));

  /** time zone suggested by the chosen country; falls back to the browser's */
  private suggestedZone: string | null = null;
  private suggestedCurrency: string | null = null;

  constructor() {
    this.followBusinessName();
    this.followCountry();
    this.checkCodeAvailability();
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
      revealErrors(form);
      return;
    }
    const timeZone = this.suggestedZone ?? Intl.DateTimeFormat().resolvedOptions().timeZone;
    this.busy.set(true);
    this.error.set(null);
    this.api.register(toSignupInput(form.getRawValue(), timeZone, this.plan()?.id ?? null, this.cycle())).subscribe({
      next: (r) => { this.busy.set(false); this.form.controls.password.reset(''); this.result.set(r); },
      error: (e) => {
        this.busy.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(form, p) : [];
        this.error.set(registrationFailureText(p, rest));
        if (form.controls.signInKey.errors?.['server']) this.codeState.set('idle');
      },
    });
  }

  /** The workspace code follows the business name until the person edits it. */
  private followBusinessName(): void {
    const controls = this.form.controls;
    controls.businessName.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((name) => {
      if (!this.codeEdited()) controls.signInKey.setValue(suggestWorkspaceCode(name));
    });
  }

  /** The chosen country suggests the time zone and the currency. */
  private followCountry(): void {
    const controls = this.form.controls;
    controls.countryCode.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((code) => {
      const c = this.countries().find((x) => x.code === code);
      if (!c) return;
      this.suggestedZone = c.defaultTimeZone;
      // Follows the country until the person picks a different currency.
      const current = controls.baseCurrency.value;
      if (!current || current === this.suggestedCurrency) controls.baseCurrency.setValue(c.defaultCurrency);
      this.suggestedCurrency = c.defaultCurrency;
    });
  }

  /** Asks the server whether a well-formed workspace code is free, a moment after the person stops typing. */
  private checkCodeAvailability(): void {
    this.form.controls.signInKey.valueChanges.pipe(
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
  }
}
