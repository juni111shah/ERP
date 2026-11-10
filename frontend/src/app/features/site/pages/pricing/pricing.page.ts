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
  template: `
    <header class="top">
      <div class="inner">
        <a routerLink="/pricing" class="brand" aria-label="Home"><erp-brand-logo [size]="logoSize"></erp-brand-logo></a>
        <nav aria-label="On this page">
          <button type="button" (click)="go('plans')">Pricing</button>
          <button type="button" (click)="go('showcase')">Product</button>
          <button type="button" (click)="go('mobile')">Mobile app</button>
          <button type="button" (click)="go('modules')">Modules</button>
          <button type="button" (click)="go('faq')">FAQ</button>
        </nav>
        <div class="top-actions">
          @if (signedIn()) {
            <a nz-button nzType="primary" routerLink="/" data-testid="pricing-workspace">Go to my workspace</a>
          } @else {
            <a class="signin" routerLink="/auth/login" data-testid="pricing-signin">Sign in</a>
            @if (open()) { <button nz-button nzType="primary" type="button" (click)="go('plans')" data-testid="pricing-top-start">Get started</button> }
          }
        </div>
      </div>
    </header>

    <section class="hero">
      <div class="inner">
        <div class="hero-copy">
          <span class="eyebrow">One system for the whole business</span>
          <h1>Run your finance, sales and teams from one place</h1>
          <p class="lead">HigherSquare 360 brings the work your company does every day into a single system, so everyone works from the same numbers. Start with what you need today and switch on more as you grow.</p>
          <div class="cta">
            @if (open()) {
              <button nz-button nzType="primary" nzSize="large" type="button" (click)="go('plans')" data-testid="pricing-hero-start">{{ trialDays() ? 'Start your ' + trialDays() + '-day free trial' : 'See the plans' }}</button>
            }
            <button nz-button nzSize="large" type="button" (click)="go('showcase')">See what it does</button>
          </div>
          <ul class="ticks">
            @if (trialDays()) { <li><span nz-icon nzType="check-circle"></span>{{ trialDays() }} days free</li> }
            <li><span nz-icon nzType="check-circle"></span>No card needed</li>
            <li><span nz-icon nzType="check-circle"></span>Change plan as you grow</li>
          </ul>
        </div>
        <div class="hero-stage" erpTilt aria-hidden="true" data-testid="pricing-hero-scene">
        <div class="scene">
        <div class="float a"><span class="f-ico" nz-icon nzType="check-circle"></span><div><strong>Invoice paid</strong><small>1,050.00 received</small></div></div>
        <div class="float b"><span class="f-ico warn" nz-icon nzType="check-square"></span><div><strong>Approval needed</strong><small>Purchase order</small></div></div>
        <div class="hero-phone"><erp-phone screen="overview" /></div>
        <div class="window">
          <div class="window-bar"><i></i><i></i><i></i></div>
          <div class="fit" erpFit><erp-web-dash /></div>
        </div>
        </div>
        </div>
      </div>
    </section>

    @if (facts().length) {
      <section class="facts" data-testid="pricing-facts">
        <div class="inner" erpInView>
          @for (f of facts(); track f.label; let i = $index) { <div class="pop" [style.--d]="i"><strong>{{ f.value }}</strong><span>{{ f.label }}</span></div> }
        </div>
      </section>
    }

    <section id="plans" class="band">
      <div class="inner">
        <div class="head">
          <h2>Simple pricing that grows with you</h2>
          <p>Pick the plan that fits the size of your business today, and move up when you need to.</p>
        </div>
        @if (loading()) {
          <div class="center"><nz-spin nzSimple></nz-spin></div>
        } @else if (failed()) {
          <erp-state-panel kind="error" title="The prices could not load" hint="Please try again in a moment." actionLabel="Try again" (action)="load()" />
        } @else if (config(); as c) {
          @if (c.mode === 'closed') {
            <div class="closed" data-testid="pricing-closed"><strong>We are not taking new registrations right now.</strong>
              @if (c.supportEmail) { <span> To get started, write to <a [href]="'mailto:' + c.supportEmail">{{ c.supportEmail }}</a>.</span> }
            </div>
          }
          @if (c.plans.length === 0) {
            <erp-state-panel title="Plans are being updated" [hint]="c.supportEmail ? 'Please write to ' + c.supportEmail + ' for prices.' : 'Please check back soon.'" />
          } @else {
            <div class="cycle">
              <nz-radio-group [ngModel]="cycle()" (ngModelChange)="cycle.set($event)" nzButtonStyle="solid" data-testid="pricing-cycle">
                <label nz-radio-button nzValue="monthly" data-testid="pricing-cycle-monthly">Monthly</label>
                <label nz-radio-button nzValue="annual" data-testid="pricing-cycle-annual">Annual</label>
              </nz-radio-group>
              @if (bestSaving(); as s) { <small>Pay annually and save up to {{ s }}%</small> }
            </div>
            <div class="plans">
              @for (p of c.plans; track p.id) {
                <erp-plan-card [plan]="p" [cycle]="cycle()" [disabled]="!open()" [actionLabel]="p.trialDays > 0 ? 'Start free trial' : 'Choose ' + p.name" (choose)="start($event)" />
              }
            </div>
            <p class="note">No card needed. When you continue after the trial, we send an invoice that you pay by bank transfer.</p>

            @if (c.plans.length > 1) {
              <h3 class="compare-title">Compare the plans</h3>
              <div class="compare" tabindex="0" role="region" aria-label="Compare the plans">
                <table data-testid="pricing-compare">
                  <thead><tr><th scope="col"></th>@for (p of c.plans; track p.id) { <th scope="col">{{ p.name }}</th> }</tr></thead>
                  <tbody>
                    <tr><th scope="row">Monthly price</th>@for (p of c.plans; track p.id) { <td>{{ money(p.monthlyPrice, p) }}</td> }</tr>
                    <tr><th scope="row">Annual price</th>@for (p of c.plans; track p.id) { <td>{{ money(p.annualPrice, p) }}</td> }</tr>
                    <tr><th scope="row">Free trial</th>@for (p of c.plans; track p.id) { <td>{{ p.trialDays > 0 ? p.trialDays + ' days' : 'None' }}</td> }</tr>
                    <tr><th scope="row">Users</th>@for (p of c.plans; track p.id) { <td>{{ users(p) }}</td> }</tr>
                    <tr><th scope="row">Legal entities</th>@for (p of c.plans; track p.id) { <td>{{ entities(p) }}</td> }</tr>
                    <tr><th scope="row">Modules</th>@for (p of c.plans; track p.id) { <td>{{ moduleWords(p) }}</td> }</tr>
                  </tbody>
                </table>
              </div>
            }
          }
        }
      </div>
    </section>

    <section id="showcase">
      <div class="inner">
        <div class="head rise" erpInView>
          <h2>What it looks like in daily work</h2>
          <p>Three examples of how the pieces fit together.</p>
        </div>

        <div class="show">
          <div>
            <h3>See the whole business on one screen</h3>
            <p>Your dashboard shows sales, what customers owe, what you owe and cash in the bank, and it flags what needs attention today. Everyone sees the numbers their role allows.</p>
            <ul class="ticks column">
              <li><span nz-icon nzType="check-circle"></span>Live figures, not last month's report</li>
              <li><span nz-icon nzType="check-circle"></span>Reports you can open, filter and export</li>
              <li><span nz-icon nzType="check-circle"></span>Instant notifications when something needs you</li>
            </ul>
          </div>
          <div class="stage3d" erpInView erpTilt aria-hidden="true">
            <div class="panel left">
              <h4 class="todo-title">Alerts and to-dos</h4>
              <div class="todo pop" style="--d:0"><span class="sign red"><span nz-icon nzType="warning"></span></span><div><div class="tags"><span class="tag">Finance</span><span class="tag red">Action needed</span></div><p>3 invoices are overdue</p></div></div>
              <div class="todo pop" style="--d:1"><span class="sign amber"><span nz-icon nzType="bulb"></span></span><div><div class="tags"><span class="tag">Inventory</span><span class="tag red">Action needed</span></div><p>1 item at or below their reorder level</p></div></div>
              <div class="todo pop" style="--d:2"><span class="sign amber"><span nz-icon nzType="check-square"></span></span><div><div class="tags"><span class="tag">Approvals</span><span class="tag amber">Upcoming</span></div><p>2 requests are waiting for your decision</p></div></div>
            </div>
          </div>
        </div>

        <div class="show flip">
          <div>
            <h3>From first quote to final payment</h3>
            <p>Each step picks up where the last one stopped, so nobody types the same order twice. See at a glance where every deal stands, and what is still to be delivered, invoiced or collected.</p>
            <ul class="ticks column">
              <li><span nz-icon nzType="check-circle"></span>Sales and purchasing work the same way</li>
              <li><span nz-icon nzType="check-circle"></span>Stock follows deliveries and receipts</li>
              <li><span nz-icon nzType="check-circle"></span>Documents print and email from the record</li>
            </ul>
          </div>
          <div class="stage3d" erpInView erpTilt aria-hidden="true">
            <div class="panel right">
            <div class="flow">
              @for (s of salesSteps; track s; let i = $index) {
                <div class="step pop" [style.--d]="i" [class.done]="i < 3" [class.now]="i === 3"><span>{{ i < 3 ? '✓' : i + 1 }}</span>{{ s }}</div>
              }
            </div>
            <div class="flow-line"><i style="width:68%"></i></div>
            <p class="flow-note">Order delivered. Ready to invoice.</p>
            </div>
          </div>
        </div>

        <div class="show">
          <div>
            <h3>Books that keep themselves up to date</h3>
            <p>Invoices, bills and payments post to your ledger as they happen, so your accounts stay current without double entry. Tax, bank matching and budgets are part of it.</p>
            <ul class="ticks column">
              <li><span nz-icon nzType="check-circle"></span>Chart of accounts ready from the first day</li>
              <li><span nz-icon nzType="check-circle"></span>Tax rates set up for your country</li>
              <li><span nz-icon nzType="check-circle"></span>Your accountant sees every entry behind a figure</li>
            </ul>
          </div>
          <div class="stage3d" erpInView erpTilt aria-hidden="true">
            <div class="panel left">
            <table class="journal">
              <thead><tr><th>Account</th><th>Debit</th><th>Credit</th></tr></thead>
              <tbody>
                <tr class="pop" style="--d:0"><td>Accounts receivable</td><td>1,050.00</td><td></td></tr>
                <tr class="pop" style="--d:1"><td>Sales revenue</td><td></td><td>1,000.00</td></tr>
                <tr class="pop" style="--d:2"><td>Output tax payable</td><td></td><td>50.00</td></tr>
              </tbody>
              <tfoot><tr class="pop" style="--d:3"><td>Balanced</td><td>1,050.00</td><td>1,050.00</td></tr></tfoot>
            </table>
            </div>
          </div>
        </div>
        <p class="illustration">The screens on this page follow the real product. Names and figures are samples.</p>
      </div>
    </section>

    <section id="mobile" class="mobile-band" data-testid="pricing-mobile">
      <div class="inner"><erp-pricing-mobile /></div>
    </section>

    <section class="band">
      <div class="inner">
        <div class="head rise" erpInView><h2>Included in every workspace</h2><p>These come with every plan, whichever modules you use.</p></div>
        <div class="cards" erpInView>
          @for (b of builtIn(); track b.title; let i = $index) {
            <div class="feature pop" [style.--d]="i"><span class="ico" nz-icon [nzType]="b.icon"></span><h3>{{ b.title }}</h3><p>{{ b.text }}</p></div>
          }
        </div>
      </div>
    </section>

    @if (modules().length) {
      <section id="modules">
        <div class="inner">
          <div class="head rise" erpInView><h2>Switch on what your business needs</h2><p>Each module adds a part of the business. Turn one off and its data stays, ready for when you need it again.</p></div>
          <div class="cards modules" erpInView data-testid="pricing-modules">
            @for (m of modules(); track m.key; let i = $index) {
              <div class="feature pop" [style.--d]="i % 6"><span class="mono">{{ m.name.charAt(0) }}</span><h3>{{ m.name }}</h3><p>{{ m.description }}</p></div>
            }
          </div>
        </div>
      </section>
    }

    @if (industries().length || packs().length) {
      <section class="band">
        <div class="inner two">
          @if (industries().length) {
            <div>
              <h2>Set up for your industry</h2>
              <p>Tell us what you do when you register, and your workspace starts with the right modules and accounts.</p>
              <div class="chips">@for (i of industries(); track i.key) { <span>{{ i.label }}</span> }</div>
            </div>
          }
          @if (packs().length) {
            <div>
              <h2>Ready for your country</h2>
              <p>Tax rates and the local details your records need are set up when you choose one of these countries.</p>
              <div class="chips">@for (c of packs(); track c.code) { <span>{{ c.name }}</span> }</div>
            </div>
          }
        </div>
      </section>
    }

    <section>
      <div class="inner">
        <div class="head"><h2>How to get started</h2></div>
        <ol class="steps" erpInView>
          <li class="pop" style="--d:0"><b>1</b><h3>Choose a plan</h3><p>Pick the one that fits today. You can change later.</p></li>
          <li class="pop" style="--d:1"><b>2</b><h3>Register your business</h3><p>Your name, business and country. It takes about a minute.</p></li>
          <li class="pop" style="--d:2"><b>3</b><h3>{{ instant() ? 'Your workspace opens' : 'We set you up' }}</h3><p>{{ instant() ? 'It comes ready with accounts, taxes and modules for your business.' : 'We check your details and email you when your workspace is ready, with accounts, taxes and modules in place.' }}</p></li>
          <li class="pop" style="--d:3"><b>4</b><h3>Bring your team</h3><p>Import your records, invite your people and start working.</p></li>
        </ol>
      </div>
    </section>

    <section class="band">
      <div class="inner">
        <div class="head"><h2>Built to be trusted with your business</h2></div>
        <div class="cards four" erpInView>
          @for (t of trust(); track t.title; let i = $index) {
            <div class="feature pop" [style.--d]="i"><span class="ico" nz-icon [nzType]="t.icon"></span><h3>{{ t.title }}</h3><p>{{ t.text }}</p></div>
          }
        </div>
      </div>
    </section>

    <section id="faq">
      <div class="inner narrow">
        <div class="head"><h2>Questions people ask</h2></div>
        <div class="faq" data-testid="pricing-faq">
          @for (f of faqs(); track f.q) {
            <details><summary>{{ f.q }}<span nz-icon nzType="down"></span></summary><p>{{ f.a }}</p></details>
          }
        </div>
        @if (supportEmail(); as mail) { <p class="ask">Something else? Write to <a [href]="'mailto:' + mail">{{ mail }}</a>.</p> }
      </div>
    </section>

    @if (open()) {
      <section class="final">
        <div class="inner">
          <h2>Ready to see it with your own business?</h2>
          <p>{{ trialDays() ? trialDays() + ' days free. No card needed.' : 'It takes about a minute to register.' }}</p>
          <button nz-button nzSize="large" type="button" (click)="go('plans')" data-testid="pricing-final-start">Choose a plan</button>
        </div>
      </section>
    }

    <footer>
      <div class="inner">
        <div class="foot-brand">
          <erp-brand-logo size="md"></erp-brand-logo>
          <p>One system for the whole business.</p>
        </div>
        <div class="foot-links">
          <button type="button" (click)="go('plans')">Pricing</button>
          <button type="button" (click)="go('mobile')">Mobile app</button>
          <button type="button" (click)="go('faq')">FAQ</button>
          <a routerLink="/auth/login">Sign in</a>
          @if (termsUrl(); as url) { <a [href]="url" target="_blank" rel="noopener" data-testid="pricing-terms">Terms of Service</a> } @else { <a routerLink="/terms" data-testid="pricing-terms">Terms of Service</a> }
          <a routerLink="/privacy" data-testid="pricing-privacy">Privacy Policy</a>
          @if (supportEmail(); as mail) { <a [href]="'mailto:' + mail">{{ mail }}</a> }
        </div>
        <p class="copyright">© {{ year }} {{ companyName() }}</p>
      </div>
    </footer>
    @defer (on idle) { <erp-brand-assistant /> }`,
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
