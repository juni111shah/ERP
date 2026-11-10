import { doneText } from '@core/format/done-text';
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
import { CurrencyOption } from '@core/api/admin.api';
import { PlanView, PlatformApi, PlatformSettingsView, SignupMode } from '@core/api/platform.api';
import { applyProblemDetailsToForm, asProblem, controlError, problemMessage } from '@core/http/problem-details';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { PLATFORM_STYLES } from '../../platform-ui';

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
  templateUrl: './platform-settings.page.html',
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
