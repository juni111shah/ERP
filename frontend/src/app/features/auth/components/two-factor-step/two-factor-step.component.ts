import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AuthService, LoginResponse, TwoFactorChallengeSetup } from '@core/auth/auth.service';
import { asProblem } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';

export type TwoFactorMode = 'setup' | 'verify';

/**
 * The second sign-in step, after a correct password, when two-factor is needed.
 * - setup: first sign-in without an authenticator. Shows the QR code of the otpauth:// link (or the key for typing in), then
 *   takes the first 6-digit code from the app; on success shows the one-time recovery codes before continuing.
 * - verify: returning user. Takes the code from the app (or a recovery code).
 * The 6-digit field submits by itself once six digits are in. Emits `completed` with the session / workspace-choice answer.
 */
@Component({
  selector: 'erp-two-factor-step',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TranslatePipe, NzAlertModule, NzButtonModule, NzIconModule, NzInputModule, NzSpinModule],
  styleUrl: './two-factor-step.component.scss',
  templateUrl: './two-factor-step.component.html',
})
export class TwoFactorStepComponent implements OnInit {
  readonly mode = input.required<TwoFactorMode>();
  readonly challengeToken = input.required<string>();
  /** The sign-in answer (session started, or a workspace choice) once the code is accepted. */
  readonly completed = output<LoginResponse>();
  readonly cancel = output<void>();

  private readonly auth = inject(AuthService);
  private readonly i18n = inject(I18nService);
  private readonly codeInput = viewChild<ElementRef<HTMLInputElement>>('codeInput');

  readonly view = signal<'code' | 'recovery' | 'expired'>('code');
  readonly setup = signal<TwoFactorChallengeSetup | null>(null);
  readonly showKey = signal(false);
  readonly code = signal('');
  readonly useRecovery = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly copied = signal<'key' | 'codes' | null>(null);
  readonly recoveryCodes = signal<string[]>([]);
  private result: LoginResponse | null = null;

  ngOnInit(): void {
    if (this.mode() === 'setup') {
      this.auth.beginTwoFactorSetup(this.challengeToken()).subscribe({
        next: (s) => { this.setup.set(s); this.focus(); },
        error: (e) => this.fail(e),
      });
    } else {
      this.focus();
    }
  }

  canSubmit(): boolean {
    const v = this.code().trim();
    return this.useRecovery() ? v.length >= 6 : /^\d{6}$/.test(v);
  }

  onCode(value: string): void {
    const v = this.useRecovery() ? value : (value ?? '').replace(/\D/g, '').slice(0, 6);
    this.code.set(v);
    const el = this.codeInput()?.nativeElement;
    if (el && el.value !== v) el.value = v;
    if (this.error() && v.length > 0) this.error.set(null);
    if (!this.useRecovery() && v.length === 6 && !this.busy()) this.submit(); // auto-submit the sixth digit (also autofill / paste)
  }

  toggleRecovery(): void {
    this.useRecovery.set(!this.useRecovery());
    this.code.set('');
    this.error.set(null);
    this.focus();
  }

  submit(): void {
    if (this.busy() || !this.canSubmit()) return;
    this.busy.set(true);
    this.error.set(null);
    this.auth.verifyTwoFactor(this.challengeToken(), this.code().trim()).subscribe({
      next: (r) => {
        this.busy.set(false);
        if (r.recoveryCodes?.length) { this.result = r; this.recoveryCodes.set(r.recoveryCodes); this.view.set('recovery'); return; }
        this.completed.emit(r);
      },
      error: (e) => { this.busy.set(false); this.fail(e); },
    });
  }

  finish(): void { if (this.result) this.completed.emit(this.result); }

  copyKey(secret: string): void {
    this.copy(secret.replace(/\s+/g, ''), 'key');
  }

  copyCodes(): void {
    this.copy(this.recoveryCodes().join('\n'), 'codes');
  }

  private copy(text: string, what: 'key' | 'codes'): void {
    const done = () => { this.copied.set(what); setTimeout(() => this.copied.set(null), 2000); };
    try {
      if (navigator.clipboard?.writeText) { navigator.clipboard.writeText(text).then(done, () => undefined); return; }
    } catch { /* clipboard blocked: the key stays visible for manual copy */ }
  }

  private fail(e: unknown): void {
    const p = asProblem(e);
    this.code.set('');
    const el = this.codeInput()?.nativeElement;
    if (el) el.value = '';
    const t = (k: string, f: string) => this.i18n.t(k, f);
    if (p?.status === 401 && /expired|sign in again/i.test(p.detail ?? '')) { this.view.set('expired'); return; }
    if (p?.status === 401) this.error.set(t('twofa.error.wrong', "That code didn't work. Check the time on your phone and enter the newest code."));
    else if (p?.status === 423) this.error.set(t('twofa.error.locked', 'Too many wrong codes. Your account is locked for a few minutes.'));
    else if (p?.status === 429) this.error.set(t('twofa.error.tooMany', 'Too many attempts. Please wait a minute and try again.'));
    else if (p?.status === 0) this.error.set(t('twofa.error.offline', 'Cannot reach the server. Check your connection.'));
    else this.error.set(p?.detail ?? t('twofa.error.generic', 'Something went wrong. Please try again.'));
    this.focus();
  }

  private focus(): void { setTimeout(() => this.codeInput()?.nativeElement.focus()); }
}
