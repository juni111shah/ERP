import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService, LoginResponse, TwoFactorChallengeSetup } from '../core/auth/auth.service';
import { asProblem } from '../core/http/problem-details';
import { I18nService } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

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
  imports: [FormsModule, TranslatePipe, NzAlertModule, NzButtonModule, NzIconModule, NzInputModule],
  styles: [`
    :host { display: block; }
    .tf-head { margin-bottom: 18px; }
    .tf-icon { width: 48px; height: 48px; border-radius: 12px; display: grid; place-items: center; font-size: 22px;
      background: #e6f7ef; color: #10955d; margin-bottom: 14px; }
    .tf-title { font-size: 24px; font-weight: 800; color: #0f172a; margin: 0 0 6px; letter-spacing: -0.02em; line-height: 1.2; }
    .tf-sub { font-size: 14px; color: #64748b; margin: 0; line-height: 1.5; }
    .tf-steps { margin: 0 0 14px; padding-inline-start: 20px; color: #334155; font-size: 13.5px; line-height: 1.6; }
    .tf-qr-card { display: flex; flex-direction: column; align-items: center; gap: 10px; padding: 16px; border-radius: 12px;
      border: 1.5px solid rgba(16, 149, 93, 0.2); background: rgba(255, 255, 255, 0.9); margin-bottom: 12px; }
    .tf-qr { width: 184px; height: 184px; image-rendering: pixelated; display: block; }
    .tf-account { font-size: 12.5px; color: #64748b; text-align: center; word-break: break-all; }
    .tf-toggle { padding-inline: 0; font-size: 13px; color: #10955d; }
    .tf-key-row { display: flex; align-items: center; gap: 8px; margin: 4px 0 14px; padding: 10px 12px; border-radius: 10px;
      background: #f4f6f8; border: 1px dashed #c8eedc; }
    .tf-key { flex: 1; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 14px; letter-spacing: 0.06em;
      color: #0f172a; word-break: break-all; direction: ltr; unicode-bidi: isolate; text-align: start; }
    .tf-label { display: block; font-size: 13px; font-weight: 600; color: #334155; margin-bottom: 6px; }
    .tf-code { height: 48px; font-size: 22px; letter-spacing: 0.45em; text-align: center; border-radius: 10px;
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; direction: ltr; }
    .tf-code.tf-recovery { letter-spacing: 0.08em; font-size: 16px; }
    .tf-alert { margin-bottom: 14px; border-radius: 8px; }
    .tf-actions { display: flex; flex-direction: column; gap: 8px; margin-top: 16px; }
    .tf-submit { height: 44px; border-radius: 10px; font-weight: 700; }
    .tf-link { padding-inline: 0; color: #64748b; font-size: 13px; }
    .tf-codes { display: grid; grid-template-columns: 1fr 1fr; gap: 6px 16px; padding: 14px 16px; border-radius: 10px; background: #f4f6f8;
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 14px; direction: ltr; margin-bottom: 12px; }
  `],
  template: `
    @switch (view()) {
      @case ('recovery') {
        <div class="tf-head">
          <div class="tf-icon"><span nz-icon nzType="check-circle" nzTheme="outline"></span></div>
          <h2 class="tf-title">{{ 'twofa.recovery.title' | translate: 'Save your recovery codes' }}</h2>
          <p class="tf-sub">{{ 'twofa.recovery.sub' | translate: 'Two-factor is on. If you lose your phone, each of these codes signs you in once. They are shown only now.' }}</p>
        </div>
        <div class="tf-codes" data-testid="twofa-recovery-codes">
          @for (c of recoveryCodes(); track c) { <span>{{ c }}</span> }
        </div>
        <div class="tf-actions">
          <button nz-button nzType="default" type="button" (click)="copyCodes()" data-testid="twofa-copy-codes">
            <span nz-icon [nzType]="copied() === 'codes' ? 'check' : 'copy'"></span>
            {{ (copied() === 'codes' ? 'twofa.copied' : 'twofa.copyCodes') | translate: (copied() === 'codes' ? 'Copied' : 'Copy codes') }}
          </button>
          <button nz-button nzType="primary" type="button" class="tf-submit" (click)="finish()" data-testid="twofa-continue">
            {{ 'twofa.continue' | translate: 'Continue' }}
          </button>
        </div>
      }
      @case ('expired') {
        <div class="tf-head">
          <div class="tf-icon"><span nz-icon nzType="clock-circle" nzTheme="outline"></span></div>
          <h2 class="tf-title">{{ 'twofa.expired.title' | translate: 'Sign in again' }}</h2>
          <p class="tf-sub">{{ 'twofa.expired.sub' | translate: 'This sign-in step has expired. Enter your email and password again to continue.' }}</p>
        </div>
        <button nz-button nzType="primary" type="button" class="tf-submit" (click)="cancel.emit()" data-testid="twofa-restart">
          {{ 'twofa.backToLogin' | translate: 'Back to sign in' }}
        </button>
      }
      @default {
        <div class="tf-head">
          <div class="tf-icon"><span nz-icon nzType="safety" nzTheme="outline"></span></div>
          @if (mode() === 'setup') {
            <h2 class="tf-title" data-testid="twofa-setup-title">{{ 'twofa.setup.title' | translate: 'Set up two-factor sign-in' }}</h2>
            <p class="tf-sub">{{ 'twofa.setup.sub' | translate: 'Your account needs an authenticator app (such as Google Authenticator or Microsoft Authenticator) to sign in.' }}</p>
          } @else {
            <h2 class="tf-title" data-testid="twofa-verify-title">{{ 'twofa.verify.title' | translate: 'Enter your verification code' }}</h2>
            <p class="tf-sub">{{ 'twofa.verify.sub' | translate: 'Open your authenticator app and enter the 6-digit code for this account.' }}</p>
          }
        </div>

        @if (mode() === 'setup') {
          <ol class="tf-steps">
            <li>{{ 'twofa.setup.step1' | translate: 'Open your authenticator app and add an account.' }}</li>
            <li>{{ 'twofa.setup.step2' | translate: 'Scan this QR code with the app.' }}</li>
            <li>{{ 'twofa.setup.step3' | translate: 'Enter the 6-digit code the app shows.' }}</li>
          </ol>
          <div class="tf-qr-card">
            @if (setup(); as s) {
              <img class="tf-qr" [src]="s.qrCodeDataUri" [attr.alt]="'twofa.setup.qrAlt' | translate: 'QR code for your authenticator app'" data-testid="twofa-qr" />
              <div class="tf-account" dir="ltr">{{ s.issuer }} · {{ s.account }}</div>
            } @else {
              <span class="erp-skel tf-qr" role="status" aria-label="Loading…" data-testid="twofa-qr-loading"></span>
            }
          </div>
          @if (setup(); as s) {
            <button nz-button nzType="link" type="button" class="tf-toggle" (click)="showKey.set(!showKey())" [attr.aria-expanded]="showKey()" data-testid="twofa-show-key">
              {{ (showKey() ? 'twofa.setup.hideKey' : 'twofa.setup.showKey') | translate: (showKey() ? 'Hide key' : "Can't scan? Show key") }}
            </button>
            @if (showKey()) {
              <div class="tf-key-row">
                <span class="tf-key" data-testid="twofa-secret" [attr.aria-label]="'twofa.setup.keyLabel' | translate: 'Setup key'">{{ s.secret }}</span>
                <button nz-button nzType="text" type="button" (click)="copyKey(s.secret)" data-testid="twofa-copy-key"
                  [attr.aria-label]="'twofa.copyKey' | translate: 'Copy key'">
                  <span nz-icon [nzType]="copied() === 'key' ? 'check' : 'copy'"></span>
                  {{ (copied() === 'key' ? 'twofa.copied' : 'twofa.copy') | translate: (copied() === 'key' ? 'Copied' : 'Copy') }}
                </button>
              </div>
            }
          }
        }

        @if (error()) {
          <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon class="tf-alert" data-testid="twofa-error"></nz-alert>
        }

        <form (ngSubmit)="submit()">
          <label class="tf-label" for="twofa-code">
            {{ (useRecovery() ? 'twofa.recoveryCode' : 'twofa.code') | translate: (useRecovery() ? 'Recovery code' : '6-digit code') }}
          </label>
          <input #codeInput nz-input id="twofa-code" name="code" class="tf-code" [class.tf-recovery]="useRecovery()"
            [ngModel]="code()" (ngModelChange)="onCode($event)"
            [attr.inputmode]="useRecovery() ? 'text' : 'numeric'" [attr.maxlength]="useRecovery() ? 24 : 6"
            [attr.pattern]="useRecovery() ? null : '[0-9]*'" autocomplete="one-time-code"
            [placeholder]="useRecovery() ? 'XXXXX-XXXXX' : '000000'" [disabled]="busy() || (mode() === 'setup' && !setup())"
            data-testid="twofa-code" />
          <div class="tf-actions">
            <button nz-button nzType="primary" class="tf-submit" [nzLoading]="busy()" [disabled]="busy() || !canSubmit()" data-testid="twofa-submit">
              {{ (mode() === 'setup' ? 'twofa.setup.submit' : 'twofa.verify.submit') | translate: (mode() === 'setup' ? 'Verify and turn on' : 'Verify and sign in') }}
            </button>
            @if (mode() === 'verify') {
              <button nz-button nzType="link" type="button" class="tf-link" (click)="toggleRecovery()" data-testid="twofa-use-recovery">
                {{ (useRecovery() ? 'twofa.useApp' : 'twofa.useRecovery') | translate: (useRecovery() ? 'Use the authenticator app instead' : 'Use a recovery code') }}
              </button>
            }
            <button nz-button nzType="link" type="button" class="tf-link" (click)="cancel.emit()" data-testid="twofa-cancel">
              {{ 'twofa.backToLogin' | translate: 'Back to sign in' }}
            </button>
          </div>
        </form>
      }
    }
  `,
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
