import { TranslatePipe } from '../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService } from '../core/auth/auth.service';
import { safeReturnUrl } from '../core/guards/access.guards';
import { asProblem } from '../core/http/problem-details';
import { Workspace } from '../core/session/session.store';
import { BrandLogoComponent } from '../shared/brand-logo.component';

@Component({
  selector: 'erp-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    RouterLink,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzCheckboxModule,
    NzAlertModule,
    NzIconModule,
    BrandLogoComponent
  ],
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      width: 100%;
      background: #ffffff;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      color: #0f172a;
    }

    .login-root {
      display: grid;
      grid-template-columns: 46% 54%;
      min-height: 100vh;
      width: 100%;
      position: relative;
    }

    /* Left Side: Authentication Panel */
    .login-left-pane {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      padding: 48px 72px;
      background: #ffffff;
      position: relative;
      z-index: 2;
    }

    .brand-top {
      display: inline-flex;
      align-items: center;
      margin-bottom: 24px;
    }

    .login-main-content {
      width: 100%;
      max-width: 440px;
      margin: auto 0;
      padding: 16px 0;
    }

    .login-heading-group {
      margin-bottom: 32px;
    }

    .login-title {
      font-size: 38px;
      font-weight: 800;
      color: #0f172a;
      letter-spacing: -0.025em;
      line-height: 1.15;
      margin: 0 0 10px 0;
    }

    .login-subtitle {
      font-size: 14.5px;
      color: #64748b;
      margin: 0;
      line-height: 1.5;
    }

    .auth-alert {
      margin-bottom: 22px;
      border-radius: 8px;
    }

    .auth-form {
      display: flex;
      flex-direction: column;
    }

    .form-item {
      margin-bottom: 20px;
    }

    .form-item ::ng-deep .ant-form-item-label > label {
      font-size: 13.5px;
      font-weight: 600;
      color: #334155;
      height: auto;
      margin-bottom: 6px;
    }

    .custom-input-group {
      border: 1.5px solid #e2e8f0;
      border-radius: 8px;
      background: #ffffff;
      transition: all 0.2s ease;
      overflow: hidden;
    }

    .custom-input-group:hover {
      border-color: #cbd5e1;
    }

    .custom-input-group:focus-within {
      border-color: #10955d;
      box-shadow: 0 0 0 3.5px rgba(16, 149, 93, 0.12);
    }

    .custom-input-group input {
      height: 44px;
      font-size: 14.5px;
      border: none;
      background: transparent;
      box-shadow: none !important;
      color: #0f172a;
    }

    .custom-input-group input::placeholder {
      color: #94a3b8;
    }

    .field-icon {
      color: #94a3b8;
      font-size: 17px;
      margin-right: 6px;
      margin-left: 4px;
    }

    .eye-btn {
      background: transparent;
      border: none;
      cursor: pointer;
      color: #64748b;
      padding: 6px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: color 0.15s;
    }

    .eye-btn:hover {
      color: #0f172a;
    }

    .aux-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: 6px 0 28px 0;
      font-size: 13.5px;
    }

    .aux-row ::ng-deep .ant-checkbox-wrapper {
      color: #475569;
      font-size: 13.5px;
    }

    .aux-row ::ng-deep .ant-checkbox-checked .ant-checkbox-inner {
      background-color: #10955d;
      border-color: #10955d;
    }

    .forgot-anchor {
      color: #10955d;
      font-weight: 600;
      text-decoration: none;
      transition: color 0.15s;
    }

    .forgot-anchor:hover {
      color: #0d7b4c;
      text-decoration: underline;
    }

    .submit-row {
      margin-top: 4px;
    }

    .login-btn {
      height: 48px;
      min-width: 170px;
      padding: 0 32px;
      font-size: 14px;
      font-weight: 700;
      letter-spacing: 0.05em;
      text-transform: uppercase;
      border-radius: 8px;
      background: linear-gradient(135deg, #10955d 0%, #0d7b4c 100%);
      border: none;
      color: #ffffff;
      box-shadow: 0 10px 20px -5px rgba(16, 149, 93, 0.42);
      transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
      cursor: pointer;
    }

    .login-btn:hover:not(:disabled) {
      background: linear-gradient(135deg, #0d7b4c 0%, #0a623d 100%);
      transform: translateY(-2px);
      box-shadow: 0 14px 26px -4px rgba(16, 149, 93, 0.52);
    }

    .login-btn:active:not(:disabled) {
      transform: translateY(0);
      box-shadow: 0 6px 14px -3px rgba(16, 149, 93, 0.4);
    }

    .brand-footer-note {
      font-size: 12px;
      color: #94a3b8;
      letter-spacing: 0.02em;
      padding-top: 24px;
    }

    /* Multi-Workspace Picker */
    .ws-header {
      text-align: left;
      margin-bottom: 24px;
    }

    .ws-icon-circle {
      width: 44px;
      height: 44px;
      border-radius: 10px;
      background: #e6f7ef;
      color: #10955d;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 22px;
      margin-bottom: 16px;
    }

    .ws-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
      margin-bottom: 20px;
    }

    .ws-card-btn {
      display: flex;
      align-items: center;
      padding: 14px 18px;
      background: #ffffff;
      border: 1.5px solid #e2e8f0;
      border-radius: 10px;
      cursor: pointer;
      text-align: left;
      transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
      width: 100%;
    }

    .ws-card-btn:hover:not(:disabled) {
      border-color: #10955d;
      background: #f0fdf4;
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(16, 149, 93, 0.12);
    }

    .ws-avatar {
      width: 36px;
      height: 36px;
      border-radius: 8px;
      background: #10955d;
      color: #ffffff;
      font-weight: 700;
      font-size: 15px;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-right: 14px;
      flex-shrink: 0;
    }

    .ws-body {
      flex: 1;
      overflow: hidden;
    }

    .ws-name {
      font-size: 14.5px;
      font-weight: 600;
      color: #0f172a;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .ws-sub {
      font-size: 12px;
      color: #64748b;
      margin-top: 2px;
    }

    .ws-caret {
      color: #94a3b8;
      font-size: 14px;
    }

    /* Right Side: Illustration Panel */
    .login-right-pane {
      position: relative;
      background: linear-gradient(135deg, #f0fdf4 0%, #f8fafc 40%, #e6f7ef 100%);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 40px;
      overflow: hidden;
      border-left: 1px solid rgba(16, 149, 93, 0.08);
    }

    .organic-blob-1 {
      position: absolute;
      top: -10%;
      right: -10%;
      width: 650px;
      height: 650px;
      border-radius: 46% 54% 65% 35% / 40% 45% 55% 60%;
      background: radial-gradient(circle, rgba(16, 149, 93, 0.18) 0%, rgba(52, 188, 134, 0.07) 50%, transparent 75%);
      filter: blur(45px);
      pointer-events: none;
      animation: floatBlob 16s ease-in-out infinite alternate;
    }

    .organic-blob-2 {
      position: absolute;
      bottom: -15%;
      left: 5%;
      width: 500px;
      height: 500px;
      border-radius: 58% 42% 48% 52% / 55% 60% 40% 45%;
      background: radial-gradient(circle, rgba(16, 185, 129, 0.14) 0%, rgba(16, 149, 93, 0.04) 55%, transparent 75%);
      filter: blur(40px);
      pointer-events: none;
    }

    .floating-pie-badge {
      position: absolute;
      top: 10%;
      left: 12%;
      z-index: 3;
      filter: drop-shadow(0 12px 20px rgba(16, 149, 93, 0.15));
      animation: floatPie 6s ease-in-out infinite alternate;
    }

    .floating-orb {
      position: absolute;
      top: 22%;
      right: 14%;
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: linear-gradient(135deg, #34bc86, #10955d);
      box-shadow: 0 6px 14px rgba(16, 149, 93, 0.35);
      animation: floatOrb 5s ease-in-out infinite alternate;
    }

    .illustration-wrapper {
      position: relative;
      z-index: 2;
      width: 100%;
      max-width: 780px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .animated-svg-img {
      width: 100%;
      max-height: 82vh;
      object-fit: contain;
      filter: drop-shadow(0 24px 48px rgba(16, 149, 93, 0.09));
    }

    @keyframes floatBlob {
      0% { transform: translate(0, 0) scale(1) rotate(0deg); }
      100% { transform: translate(-30px, 40px) scale(1.08) rotate(15deg); }
    }

    @keyframes floatPie {
      0% { transform: translateY(0px) rotate(0deg); }
      100% { transform: translateY(-16px) rotate(8deg); }
    }

    @keyframes floatOrb {
      0% { transform: translateY(0px); }
      100% { transform: translateY(-12px); }
    }

    /* Responsive */
    @media (max-width: 1080px) {
      .login-root {
        grid-template-columns: 1fr;
      }
      .login-left-pane {
        padding: 40px 32px;
        min-height: 100vh;
      }
      .login-main-content {
        margin: 24px auto;
      }
      .login-right-pane {
        display: none;
      }
    }
  `],
  template: `
    <div class="login-root">
      <!-- Left Panel: Authentication -->
      <section class="login-left-pane">
        <div class="brand-top" data-testid="login-brand">
          <erp-brand-logo size="md"></erp-brand-logo>
        </div>

        <div class="login-main-content">
          @if (!workspaces()) {
            <div class="login-heading-group">
              <h1 class="login-title">{{ 'login.title' | translate: 'Login' }}</h1>
              <p class="login-subtitle">Welcome to log in to your background management system.</p>
            </div>

            @if (error()) {
              <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon class="auth-alert" data-testid="login-error"></nz-alert>
            }

            <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()" class="auth-form">
              <nz-form-item class="form-item">
                <nz-form-label nzFor="email" nzRequired>{{ 'common.email' | translate: 'Email' }}</nz-form-label>
                <nz-form-control nzErrorTip="Enter your work email address">
                  <nz-input-group [nzPrefix]="emailPrefix" class="custom-input-group">
                    <input nz-input id="email" type="email" formControlName="email" autocomplete="username" placeholder="name@company.com" data-testid="login-email" />
                  </nz-input-group>
                  <ng-template #emailPrefix><span nz-icon nzType="mail" class="field-icon"></span></ng-template>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item class="form-item">
                <nz-form-label nzFor="password" nzRequired>{{ 'common.password' | translate: 'Password' }}</nz-form-label>
                <nz-form-control nzErrorTip="Enter your password">
                  <nz-input-group [nzPrefix]="pwdPrefix" [nzSuffix]="pwdSuffix" class="custom-input-group">
                    <input nz-input id="password" [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" placeholder="Please enter your password" data-testid="login-password" />
                  </nz-input-group>
                  <ng-template #pwdPrefix><span nz-icon nzType="lock" class="field-icon"></span></ng-template>
                  <ng-template #pwdSuffix>
                    <button type="button" class="eye-btn" (click)="showPassword.set(!showPassword())" [attr.aria-pressed]="showPassword()" data-testid="login-password-toggle" [title]="showPassword() ? 'Hide password' : 'Show password'">
                      <span nz-icon [nzType]="showPassword() ? 'eye-invisible' : 'eye'"></span>
                    </button>
                  </ng-template>
                </nz-form-control>
              </nz-form-item>

              @if (needsCode()) {
                <nz-form-item class="form-item">
                  <nz-form-label nzFor="code" nzRequired>{{ 'login.code' | translate: 'Verification code' }}</nz-form-label>
                  <nz-form-control nzExtra="Enter the 6-digit code from your authenticator app, or a recovery code.">
                    <nz-input-group [nzPrefix]="codePrefix" class="custom-input-group">
                      <input nz-input id="code" formControlName="code" autocomplete="one-time-code" inputmode="numeric" maxlength="6" placeholder="123456" data-testid="login-code" />
                    </nz-input-group>
                    <ng-template #codePrefix><span nz-icon nzType="safety-certificate" class="field-icon"></span></ng-template>
                  </nz-form-control>
                </nz-form-item>
              }

              <div class="aux-row">
                <label nz-checkbox formControlName="rememberMe" data-testid="login-remember">
                  {{ 'login.remember' | translate: 'Keep me signed in' }}
                </label>
                <a routerLink="/auth/forgot-password" class="forgot-anchor" data-testid="login-forgot">
                  {{ 'login.forgot' | translate: 'Forgot password?' }}
                </a>
              </div>

              <div class="submit-row">
                <button nz-button nzType="primary" [nzLoading]="busy()" [disabled]="busy() || retryIn() > 0" class="login-btn" data-testid="login-submit">
                  @if (retryIn() > 0) {
                    <span>Wait {{ retryIn() }}s</span>
                  } @else {
                    <span>LOGIN</span>
                  }
                </button>
              </div>
            </form>
          } @else {
            <!-- Multi-Workspace Picker -->
            <div class="ws-header">
              <div class="ws-icon-circle">
                <span nz-icon nzType="appstore" nzTheme="outline"></span>
              </div>
              <h2 class="login-title" style="font-size: 26px;">{{ 'login.chooseWorkspace' | translate: 'Choose a workspace' }}</h2>
              <p class="login-subtitle">Your credentials belong to several enterprise workspaces. Choose which one to enter:</p>
            </div>

            <div class="ws-list" data-testid="workspace-picker">
              @for (w of workspaces(); track w.id) {
                <button type="button" class="ws-card-btn" (click)="pick(w)" [disabled]="busy()" [attr.data-testid]="'workspace-' + w.code">
                  <div class="ws-avatar">{{ w.name.charAt(0).toUpperCase() }}</div>
                  <div class="ws-body">
                    <div class="ws-name">{{ w.name }}</div>
                    <div class="ws-sub">{{ w.industry || 'General Business' }} · <span style="font-family:monospace">{{ w.code }}</span></div>
                  </div>
                  <span nz-icon nzType="right" class="ws-caret"></span>
                </button>
              }
            </div>

            <button nz-button nzType="text" (click)="workspaces.set(null)" style="color:#64748b; font-size:13.5px; padding-left: 0;">
              ← Back to login
            </button>
          }
        </div>

        <div class="brand-footer-note">
          Enterprise Cloud Infrastructure · Sovereign & Multi-Tenant
        </div>
      </section>

      <!-- Right Panel: Brand Themed Illustration -->
      <section class="login-right-pane">
        <div class="organic-blob-1"></div>
        <div class="organic-blob-2"></div>
        <div class="floating-pie-badge">
          <svg width="56" height="56" viewBox="0 0 48 48" fill="none">
            <circle cx="24" cy="24" r="20" fill="#e6f7ef" />
            <path d="M24 4 A20 20 0 0 1 44 24 L24 24 Z" fill="#10955d" opacity="0.9" />
            <path d="M24 24 L44 24 A20 20 0 0 1 24 44 Z" fill="#34bc86" opacity="0.75" />
            <path d="M24 24 L24 44 A20 20 0 0 1 4 24 Z" fill="#0d7b4c" opacity="0.6" />
          </svg>
        </div>
        <div class="floating-orb"></div>

        <div class="illustration-wrapper">
          <img
            src="assets/data-dashboard.svg"
            alt="Business Analytics Dashboard"
            class="animated-svg-img" />
        </div>
      </section>
    </div>
  `,
})
export class LoginPage {
  readonly returnUrl = input<string>();
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    rememberMe: [false],
    code: [''],
  });
  readonly showPassword = signal(false);
  readonly needsCode = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly retryIn = signal(0);
  readonly workspaces = signal<Workspace[] | null>(null);
  private selectionToken = '';

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => c.updateValueAndValidity()); return; }
    const { email, password, rememberMe, code } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(email, password, rememberMe, code || undefined).subscribe({
      next: (r) => {
        this.busy.set(false);
        if ((r as { requiresTwoFactor?: boolean }).requiresTwoFactor) { this.needsCode.set(true); return; }
        if (r.requiresWorkspaceSelection) { this.selectionToken = r.selectionToken!; this.workspaces.set(r.workspaces); }
        else this.done();
      },
      error: (e) => { this.busy.set(false); this.fail(e); },
    });
  }

  pick(w: Workspace): void {
    this.busy.set(true);
    this.auth.selectWorkspace(this.selectionToken, w.id).subscribe({
      next: () => this.done(),
      error: (e) => { this.busy.set(false); this.workspaces.set(null); this.fail(e); },
    });
  }

  private done(): void {
    void this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
  }

  private fail(e: unknown): void {
    const p = asProblem(e);
    if (!this.needsCode()) this.form.controls.password.reset('');
    else this.form.controls.code.reset('');
    if (p?.status === 429) {
      const retry = Number((p as { retryAfterSeconds?: number }).retryAfterSeconds ?? 60);
      this.error.set('Too many attempts. Please wait before trying again.');
      this.countdown(retry);
    } else {
      this.error.set(p?.status === 0 ? 'Cannot reach the server. Check your connection.' : (p?.detail ?? 'Incorrect email or password.'));
    }
  }

  private countdown(seconds: number): void {
    this.retryIn.set(seconds);
    const t = setInterval(() => { this.retryIn.update((s) => s - 1); if (this.retryIn() <= 0) clearInterval(t); }, 1000);
  }
}
