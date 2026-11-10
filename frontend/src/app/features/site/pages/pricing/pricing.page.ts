import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { catchError, of } from 'rxjs';
import { CountryDto } from '@core/api/admin.api';
import { BillingCycle, IndustryOption, LegalDocuments, PlatformApi, PublicContent, PublicModule, PublicPlan, SignupConfig } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { limitText, yearlySaving } from '@core/format/subscription';
import { SessionStore } from '@core/session/session.store';
import { BrandAssistantComponent } from '@shared/components/brand-assistant/brand-assistant.component';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { PlanCardComponent } from '@shared/components/plan-card/plan-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { FitDirective, InViewDirective, TiltDirective } from '../../pricing-fx';
import { PhoneComponent, PricingMobileComponent } from '../../components/pricing-mobile/pricing-mobile.component';
import { WebDashComponent } from '../../components/pricing-web-dash/pricing-web-dash.component';
import { PRICING_STYLES } from '../../pricing.styles';

const SALES_STEPS = ['Quote', 'Order', 'Delivery', 'Invoice', 'Payment'];

/** The front door: what the product does, what it costs and the answers people look for before they register. Signed-out visitors land here. */
@Component({
  selector: 'erp-pricing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BrandAssistantComponent, FormsModule, RouterLink, NzButtonModule, NzIconModule, NzRadioModule, NzSpinModule, BrandLogoComponent, PlanCardComponent, StatePanelComponent, FitDirective, InViewDirective, TiltDirective, PhoneComponent, PricingMobileComponent, WebDashComponent],
  styles: [PRICING_STYLES],
  templateUrl: './pricing.page.html',
})
export class PricingPage {
  private readonly api = inject(PlatformApi);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);

  readonly salesSteps = SALES_STEPS;
  readonly year = new Date().getFullYear();
  /** The full-size logo does not fit beside the sign-in link on a phone. */
  readonly logoSize = window.innerWidth < 480 ? 'md' : 'lg';

  readonly signedIn = this.session.isAuthenticated;
  readonly config = signal<SignupConfig | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly cycle = signal<BillingCycle>('monthly');

  // The page still makes sense without any of these, so a failed call only hides its section.
  private readonly content = toSignal(this.api.publicContent().pipe(catchError(() => of(null as PublicContent | null))), { initialValue: null as PublicContent | null });
  private readonly allModules = toSignal(this.api.publicModules().pipe(catchError(() => of([] as PublicModule[]))), { initialValue: [] as PublicModule[] });
  private readonly allIndustries = toSignal(this.api.industries().pipe(catchError(() => of([] as IndustryOption[]))), { initialValue: [] as IndustryOption[] });
  private readonly countries = toSignal(this.api.countries().pipe(catchError(() => of([] as CountryDto[]))), { initialValue: [] as CountryDto[] });
  private readonly legal = toSignal(this.api.legal().pipe(catchError(() => of(null as LegalDocuments | null))), { initialValue: null as LegalDocuments | null });

  readonly builtIn = computed(() => this.content()?.builtIn ?? []);
  readonly trust = computed(() => this.content()?.trust ?? []);
  readonly faqs = computed(() => this.content()?.faqs ?? []);
  readonly modules = computed(() => this.allModules().filter((m) => !m.isCore));
  readonly industries = computed(() => this.allIndustries().filter((i) => (i.availability ?? '').toLowerCase() === 'available' && i.key !== 'generic'));
  readonly packs = computed(() => this.countries().filter((c) => c.hasPack));
  readonly open = computed(() => this.config()?.mode !== 'closed');
  readonly instant = computed(() => this.config()?.mode === 'open');
  readonly supportEmail = computed(() => this.config()?.supportEmail ?? null);
  readonly termsUrl = computed(() => this.config()?.termsUrl ?? null);
  readonly companyName = computed(() => this.legal()?.companyName || 'HigherSquare 360');

  /** The longest trial on offer, for the headline. */
  readonly trialDays = computed(() => Math.max(0, ...(this.config()?.plans ?? []).map((p) => p.trialDays)));
  readonly bestSaving = computed(() => {
    const best = Math.max(0, ...(this.config()?.plans ?? []).map((p) => yearlySaving(p.monthlyPrice, p.annualPrice)?.percent ?? 0));
    return best > 0 ? best : null;
  });

  /** Counted from what is really installed, so these never go out of date or overstate. */
  readonly facts = computed(() => {
    const list: { value: string; label: string }[] = [];
    if (this.modules().length) list.push({ value: String(this.modules().length), label: 'business modules' });
    if (this.industries().length) list.push({ value: String(this.industries().length), label: 'industry setups' });
    if (this.packs().length) list.push({ value: String(this.packs().length), label: 'country setups' });
    if (list.length) list.push({ value: 'Web + mobile', label: 'work from anywhere' });
    return list;
  });

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.signupConfig().subscribe({
      next: (c) => { this.config.set(c); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  /** In-page jump: an ordinary #link would reload the app, because of the page's base address. */
  go(id: string): void { document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' }); }

  start(plan: PublicPlan): void {
    void this.router.navigate(['/register'], { queryParams: { plan: plan.code, cycle: this.cycle() } });
  }

  money(amount: number, plan: PublicPlan): string { return displayMoney(amount, plan.currency); }
  users(plan: PublicPlan): string { return plan.maxUsers ? `Up to ${plan.maxUsers}` : 'No limit'; }
  entities(plan: PublicPlan): string { return plan.maxLegalEntities ? limitText(plan.maxLegalEntities, 'entity', 'entities') : 'No limit'; }
  moduleWords(plan: PublicPlan): string { return plan.modules.length ? `${plan.modules.length} included` : 'All included'; }
}
