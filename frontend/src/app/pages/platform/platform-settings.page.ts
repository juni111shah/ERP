import { doneText } from '../../core/format/done-text';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { forkJoin } from 'rxjs';
import { CurrencyOption } from '../../core/api/admin.api';
import { PlanView, PlatformApi, PlatformSettingsView, SignupMode } from '../../core/api/platform.api';
import { applyProblemDetailsToForm, asProblem, controlError, problemMessage } from '../../core/http/problem-details';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { PLATFORM_STYLES } from './platform-ui';

/** How the business runs: who may register, how billing behaves, and what clients see on their invoices. */
@Component({
  selector: 'erp-platform-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzAlertModule, NzButtonModule, NzFormModule, NzInputModule, NzInputNumberModule, NzRadioModule, NzSelectModule, NzSpinModule, NzSwitchModule, StatePanelComponent],
  styles: [PLATFORM_STYLES, `
    .group { margin-bottom: 16px; }
    .two { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); column-gap: 16px; }
    .two .wide { grid-column: 1 / -1; }
    .mode { display: block; margin: 0 0 8px; white-space: normal; }
    .mode small { display: block; color: var(--erp-color-text-muted); margin-inline-start: 24px; }
    .read { padding: 5px 0; font-weight: 600; color: var(--erp-color-text-primary); }
    nz-input-number { width: 100%; }
    .save-row { display: flex; justify-content: flex-end; }
    @media (max-width: 767px) { .two { grid-template-columns: minmax(0, 1fr); } }
  `],
  template: `
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the settings" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
    } @else if (!settings()) {
      <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
    } @else {
      <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="save()" data-testid="settings-form" novalidate>
        <section class="pf-card group" data-testid="settings-registration">
          <h2>Registration</h2>
          @if (!settings()!.emailServerConfigured) {
            <nz-alert nzType="warning" nzShowIcon style="margin-bottom:16px" data-testid="settings-email-off"
              nzMessage="Email is off" nzDescription="No email server is set up, so nobody can confirm an email address. Registrations go to your review queue, and clients get no email from us."></nz-alert>
          }
          <nz-form-item>
            <nz-form-label>Who can register</nz-form-label>
            <nz-form-control [nzErrorTip]="err('signupMode')">
              <nz-radio-group formControlName="signupMode" data-testid="settings-mode">
                @for (m of modes; track m.value) {
                  <label nz-radio class="mode" [nzValue]="m.value" [attr.data-testid]="'settings-mode-' + m.value">{{ m.label }}<small>{{ m.hint }}</small></label>
                }
              </nz-radio-group>
            </nz-form-control>
          </nz-form-item>
          <div class="two">
            <nz-form-item>
              <nz-form-label>Require email verification</nz-form-label>
              <nz-form-control nzExtra="They must open a link from their email before anything else happens.">
                <nz-switch formControlName="requireEmailVerification" data-testid="settings-verify"></nz-switch>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Default plan</nz-form-label>
              <nz-form-control [nzErrorTip]="err('defaultPlanId')" nzExtra="Used when a registration does not name a plan.">
                <nz-select formControlName="defaultPlanId" nzAllowClear nzPlaceHolder="No default" data-testid="settings-default-plan">
                  @for (p of plans(); track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.name"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
          </div>
        </section>

        <section class="pf-card group" data-testid="settings-billing">
          <h2>Billing</h2>
          <div class="two">
            <nz-form-item>
              <nz-form-label nzRequired>Currency</nz-form-label>
              <nz-form-control [nzErrorTip]="err('currency')" nzExtra="All plans and invoices are in this currency.">
                <nz-select formControlName="currency" nzShowSearch data-testid="settings-currency">
                  @for (c of currencies(); track c.code) { <nz-option [nzValue]="c.code" [nzLabel]="c.code + ' · ' + c.name"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Invoice prefix</nz-form-label>
              <nz-form-control [nzErrorTip]="err('invoicePrefix')" nzExtra="Starts every invoice number, up to 10 letters or digits.">
                <input nz-input formControlName="invoicePrefix" maxlength="10" data-testid="settings-prefix" />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Tax percent</nz-form-label>
              <nz-form-control [nzErrorTip]="err('taxPercent')" nzExtra="Added to every new invoice. Use 0 for none.">
                <nz-input-number formControlName="taxPercent" [nzMin]="0" [nzMax]="100" [nzPrecision]="2" data-testid="settings-tax"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Tax label</nz-form-label>
              <nz-form-control [nzErrorTip]="err('taxLabel')" nzExtra="The name of the tax on your invoices.">
                <input nz-input formControlName="taxLabel" maxlength="40" data-testid="settings-tax-label" />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Next invoice number</nz-form-label>
              <nz-form-control nzExtra="Counts up by itself, so it cannot be edited.">
                <div class="read" data-testid="settings-next-number">{{ nextNumber() }}</div>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Invoice due days</nz-form-label>
              <nz-form-control [nzErrorTip]="err('invoiceDueDays')" nzExtra="How long a client has to pay a new invoice.">
                <nz-input-number formControlName="invoiceDueDays" [nzMin]="0" [nzMax]="120" [nzPrecision]="0" data-testid="settings-due-days"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Renewal lead days</nz-form-label>
              <nz-form-control [nzErrorTip]="err('renewalLeadDays')" nzExtra="How long before a period ends the renewal invoice goes out.">
                <nz-input-number formControlName="renewalLeadDays" [nzMin]="0" [nzMax]="60" [nzPrecision]="0" data-testid="settings-lead-days"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Grace days</nz-form-label>
              <nz-form-control [nzErrorTip]="err('graceDays')" nzExtra="How long after a missed payment the client can still make changes.">
                <nz-input-number formControlName="graceDays" [nzMin]="0" [nzMax]="120" [nzPrecision]="0" data-testid="settings-grace-days"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Suspend after days</nz-form-label>
              <nz-form-control [nzErrorTip]="err('suspendAfterDays')" nzExtra="How long after a missed payment the workspace is suspended. Must be longer than the grace days.">
                <nz-input-number formControlName="suspendAfterDays" [nzMin]="1" [nzMax]="365" [nzPrecision]="0" data-testid="settings-suspend-days"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
          </div>
        </section>

        <section class="pf-card group" data-testid="settings-company">
          <h2>Your company</h2>
          <p class="pf-muted" style="margin:-4px 0 12px">Shown to clients on their billing page and on invoices.</p>
          <div class="two">
            <nz-form-item>
              <nz-form-label>Company name</nz-form-label>
              <nz-form-control [nzErrorTip]="err('companyName')"><input nz-input formControlName="companyName" data-testid="settings-company-name" /></nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Tax number</nz-form-label>
              <nz-form-control [nzErrorTip]="err('companyTaxNumber')"><input nz-input formControlName="companyTaxNumber" data-testid="settings-company-tax" /></nz-form-control>
            </nz-form-item>
            <nz-form-item class="wide">
              <nz-form-label>Address</nz-form-label>
              <nz-form-control [nzErrorTip]="err('companyAddress')"><textarea nz-input rows="2" formControlName="companyAddress" data-testid="settings-company-address"></textarea></nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Support email</nz-form-label>
              <nz-form-control [nzErrorTip]="err('supportEmail')"><input nz-input type="email" formControlName="supportEmail" data-testid="settings-support-email" /></nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Terms on another site (optional)</nz-form-label>
              <nz-form-control [nzErrorTip]="err('termsUrl')" nzExtra="Leave empty to use the terms below."><input nz-input formControlName="termsUrl" placeholder="https://" data-testid="settings-terms" /></nz-form-control>
            </nz-form-item>
            <nz-form-item class="wide">
              <nz-form-label>Payment instructions</nz-form-label>
              <nz-form-control [nzErrorTip]="err('paymentInstructions')" nzExtra="How to pay you, such as bank details. Clients see this on invoices and their billing page.">
                <textarea nz-input rows="4" formControlName="paymentInstructions" maxlength="2000" data-testid="settings-instructions"></textarea>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item class="wide">
              <nz-form-label>About your company</nz-form-label>
              <nz-form-control [nzErrorTip]="err('aboutText')" nzExtra="A few lines the public AI assistant can use when people ask about you. Leave it empty and the assistant says it does not know.">
                <textarea nz-input rows="4" formControlName="aboutText" maxlength="2000" data-testid="settings-about"></textarea>
              </nz-form-control>
            </nz-form-item>
            @if (settings()?.legalIsStarterText) {
              <nz-alert class="wide" nzType="warning" nzShowIcon data-testid="settings-legal-starter" nzMessage="The terms and privacy policy below are starter wording. Have a lawyer review them for your country, and fill in your company name and support email above, before you sell." style="margin-bottom:16px"></nz-alert>
            }
            <nz-form-item class="wide">
              <nz-form-label>Terms of Service</nz-form-label>
              <nz-form-control [nzErrorTip]="err('termsText')" [nzExtra]="legalHint">
                <textarea nz-input rows="12" formControlName="termsText" data-testid="settings-terms-text"></textarea>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item class="wide">
              <nz-form-label>Privacy Policy</nz-form-label>
              <nz-form-control [nzErrorTip]="err('privacyText')" [nzExtra]="legalHint">
                <textarea nz-input rows="12" formControlName="privacyText" data-testid="settings-privacy-text"></textarea>
              </nz-form-control>
            </nz-form-item>
            <ng-template #legalHint>Shown at <a routerLink="/terms" target="_blank">/terms</a> and <a routerLink="/privacy" target="_blank">/privacy</a> after you save. Start a line with ## to make it a heading. {{ '{' }}company{{ '}' }} and {{ '{' }}email{{ '}' }} are filled in from above. Empty the box to go back to the starter wording.</ng-template>
          </div>
        </section>
        <div class="save-row"><button nz-button nzType="primary" [nzLoading]="saving()" data-testid="settings-save">Save settings</button></div>
      </form>
    }`,
})
export class PlatformSettingsPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);

  readonly modes: { value: SignupMode; label: string; hint: string }[] = [
    { value: 'open', label: 'Open', hint: 'Anyone gets a workspace straight away.' },
    { value: 'review', label: 'Review', hint: 'You approve each one before a workspace is created.' },
    { value: 'closed', label: 'Closed', hint: 'The registration page says registration is closed and shows your support email.' },
  ];
  readonly settings = signal<PlatformSettingsView | null>(null);
  readonly nextNumber = signal(1);
  readonly plans = signal<PlanView[]>([]);
  readonly currencies = signal<CurrencyOption[]>([]);
  readonly failed = signal(false);
  readonly saving = signal(false);
  readonly form = inject(FormBuilder).group({
    signupMode: ['review' as SignupMode, Validators.required],
    requireEmailVerification: [false],
    defaultPlanId: [null as string | null],
    currency: ['', Validators.required],
    taxPercent: [0, Validators.required],
    taxLabel: ['', Validators.required],
    invoicePrefix: ['', Validators.required],
    invoiceDueDays: [0, Validators.required],
    renewalLeadDays: [0, Validators.required],
    graceDays: [0, Validators.required],
    suspendAfterDays: [1, Validators.required],
    companyName: [''],
    companyAddress: [null as string | null],
    companyTaxNumber: [null as string | null],
    supportEmail: [null as string | null],
    termsUrl: [null as string | null],
    paymentInstructions: [null as string | null],
    aboutText: [null as string | null],
    termsText: [''],
    privacyText: [''],
  });

  constructor() { this.load(); }

  err(name: string): string { return controlError(this.form.get(name)); }

  private show(s: PlatformSettingsView): void {
    this.settings.set(s);
    this.nextNumber.set(s.nextInvoiceNumber);
    const { emailServerConfigured: _email, nextInvoiceNumber: _next, legalIsStarterText: _starter, ...values } = s;
    this.form.reset(values);
  }

  load(): void {
    this.failed.set(false);
    forkJoin({ settings: this.api.settings(), plans: this.api.plans(), currencies: this.api.currencies() }).subscribe({
      next: (r) => { this.plans.set(r.plans.filter((p) => p.isActive)); this.currencies.set(r.currencies); this.show(r.settings); },
      error: () => this.failed.set(true),
    });
  }

  save(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); });
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.saveSettings({
      signupMode: v.signupMode!, requireEmailVerification: !!v.requireEmailVerification, defaultPlanId: v.defaultPlanId, currency: v.currency!, taxPercent: v.taxPercent ?? 0, taxLabel: v.taxLabel!, invoicePrefix: v.invoicePrefix!,
      invoiceDueDays: v.invoiceDueDays ?? 0, renewalLeadDays: v.renewalLeadDays ?? 0, graceDays: v.graceDays ?? 0, suspendAfterDays: v.suspendAfterDays ?? 1,
      companyName: v.companyName ?? '', companyAddress: v.companyAddress || null, companyTaxNumber: v.companyTaxNumber || null, supportEmail: v.supportEmail || null, termsUrl: v.termsUrl || null, paymentInstructions: v.paymentInstructions || null, aboutText: v.aboutText ?? '', termsText: v.termsText ?? '', privacyText: v.privacyText ?? '',
    }).subscribe({
      next: (s) => { this.saving.set(false); this.show(s); this.message.success(doneText('saved', 'Platform settings')); },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        const rest = problem ? applyProblemDetailsToForm(this.form, problem) : [];
        if (!problem?.errors || rest.length) this.message.error(rest[0] ?? problemMessage(e, 'Could not save the settings'));
      },
    });
  }
}
