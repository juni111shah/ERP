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
  imports: [TranslatePipe, ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzCheckboxModule, NzAlertModule, NzIconModule, BrandLogoComponent],
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      width: 100%;
      background: #f8fafc;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
    }

    .login-container {
      display: grid;
      grid-template-columns: 1.15fr 1fr;
      min-height: 100vh;
      width: 100%;
    }

    /* Left Hero / Brand Showcase Panel */
    .hero-panel {
      position: relative;
      background: linear-gradient(145deg, #051610 0%, #0a241b 50%, #0e3024 100%);
      color: #ffffff;
      padding: 56px 64px;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      overflow: hidden;
      border-right: 1px solid rgba(255, 255, 255, 0.08);
    }

    .ambient-glow-1 {
      position: absolute;
      top: -15%;
      left: -10%;
      width: 600px;
      height: 600px;
      background: radial-gradient(circle, rgba(16, 149, 93, 0.28) 0%, rgba(16, 149, 93, 0) 70%);
      filter: blur(50px);
      pointer-events: none;
    }

    .ambient-glow-2 {
      position: absolute;
      bottom: -10%;
      right: -10%;
      width: 550px;
      height: 550px;
      background: radial-gradient(circle, rgba(52, 211, 153, 0.16) 0%, rgba(16, 149, 93, 0) 70%);
      filter: blur(60px);
      pointer-events: none;
    }

    .grid-overlay {
      position: absolute;
      inset: 0;
      background-image: 
        linear-gradient(to right, rgba(255, 255, 255, 0.03) 1px, transparent 1px),
        linear-gradient(to bottom, rgba(255, 255, 255, 0.03) 1px, transparent 1px);
      background-size: 36px 36px;
      mask-image: radial-gradient(ellipse 80% 80% at 50% 50%, #000 30%, transparent 80%);
      pointer-events: none;
    }

    .hero-inner {
      position: relative;
      z-index: 2;
      display: flex;
      flex-direction: column;
      height: 100%;
      justify-content: space-between;
    }

    .hero-brand-wrap {
      display: inline-flex;
    }

    .status-chip {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 6px 14px;
      border-radius: 9999px;
      background: rgba(16, 149, 93, 0.2);
      border: 1px solid rgba(16, 149, 93, 0.4);
      color: #6ee7b7;
      font-size: 12.5px;
      font-weight: 600;
      letter-spacing: 0.02em;
      margin-bottom: 24px;
      backdrop-filter: blur(8px);
      width: fit-content;
    }

    .pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #10b981;
      box-shadow: 0 0 10px #10b981;
      animation: pulse 2s infinite;
    }

    @keyframes pulse {
      0% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7); }
      70% { transform: scale(1); box-shadow: 0 0 0 7px rgba(16, 185, 129, 0); }
      100% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
    }

    .hero-heading {
      font-size: 38px;
      font-weight: 800;
      line-height: 1.18;
      letter-spacing: -0.03em;
      color: #ffffff;
      margin: 0 0 18px 0;
      max-width: 540px;
    }

    .hero-heading .gradient-text {
      background: linear-gradient(135deg, #a7f3d0 0%, #34d399 50%, #10b981 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }

    .hero-sub {
      font-size: 15.5px;
      line-height: 1.6;
      color: #94a3b8;
      max-width: 490px;
      margin: 0 0 36px 0;
    }

    /* Floating Glass Telemetry Card */
    .glass-card {
      background: rgba(15, 39, 31, 0.65);
      backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 16px;
      padding: 22px 24px;
      box-shadow: 0 20px 40px -15px rgba(0, 0, 0, 0.45);
      margin-bottom: 32px;
      max-width: 520px;
    }

    .glass-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 12px;
      margin-bottom: 16px;
    }

    .glass-title-group {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      font-weight: 600;
      color: #e2e8f0;
    }

    .glass-live {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 11.5px;
      color: #34d399;
      font-weight: 600;
      background: rgba(16, 149, 93, 0.25);
      padding: 2px 8px;
      border-radius: 9999px;
    }

    .metrics-row {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 16px;
    }

    .metric-col {
      display: flex;
      flex-direction: column;
    }

    .metric-kpi {
      font-size: 18px;
      font-weight: 700;
      color: #ffffff;
      margin: 2px 0;
    }

    .metric-lbl {
      font-size: 11.5px;
      color: #94a3b8;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .metric-status {
      font-size: 11.5px;
      color: #34d399;
      display: flex;
      align-items: center;
      gap: 4px;
      margin-top: 2px;
    }

    .hero-bottom-text {
      font-size: 12px;
      color: #64748b;
      letter-spacing: 0.02em;
    }

    /* Right Auth Form Panel */
    .auth-panel {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 48px 32px;
      background: #f8fafc;
      overflow-y: auto;
    }

    .auth-card {
      width: 100%;
      max-width: 440px;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 38px 40px;
      box-shadow: 0 4px 20px -2px rgba(0, 0, 0, 0.05), 0 2px 6px -1px rgba(0, 0, 0, 0.03);
      transition: all 0.2s ease;
    }

    .mobile-logo {
      display: none;
      margin-bottom: 24px;
      justify-content: center;
    }

    .auth-header {
      margin-bottom: 28px;
    }

    .auth-title {
      font-size: 24px;
      font-weight: 800;
      letter-spacing: -0.02em;
      color: #0f172a;
      margin: 0 0 6px 0;
    }

    .auth-desc {
      font-size: 14px;
      color: #64748b;
      margin: 0;
      line-height: 1.5;
    }

    .auth-alert {
      margin-bottom: 20px;
      border-radius: 8px;
    }

    /* Form Fields */
    .auth-form nz-form-item {
      margin-bottom: 20px;
    }

    .auth-form nz-form-label {
      font-size: 13px;
      font-weight: 600;
      color: #334155;
      padding-bottom: 4px;
    }

    .auth-input-group {
      border-radius: 8px;
      overflow: hidden;
      transition: all 0.2s;
    }

    .auth-input-group input {
      height: 42px;
      font-size: 14px;
    }

    .field-icon {
      color: #94a3b8;
      font-size: 16px;
      margin-right: 4px;
    }

    .eye-btn {
      background: transparent;
      border: none;
      cursor: pointer;
      color: #64748b;
      padding: 4px;
      display: flex;
      align-items: center;
      transition: color 0.15s;
    }

    .eye-btn:hover {
      color: #0f172a;
    }

    .aux-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin: 8px 0 24px 0;
      font-size: 13px;
    }

    .forgot-anchor {
      color: #10955d;
      font-weight: 600;
      text-decoration: none;
      transition: color 0.15s;
    }

    .forgot-anchor:hover {
      color: #0c7648;
      text-decoration: underline;
    }

    .sign-in-btn {
      height: 44px;
      font-size: 15px;
      font-weight: 600;
      border-radius: 8px;
      background: #10955d;
      border-color: #10955d;
      box-shadow: 0 4px 12px rgba(16, 149, 93, 0.28);
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
    }

    .sign-in-btn:hover:not(:disabled) {
      background: #0d7c4d;
      border-color: #0d7c4d;
      box-shadow: 0 6px 16px rgba(16, 149, 93, 0.36);
      transform: translateY(-1px);
    }


    /* Workspace Picker Container */
    .ws-header {
      text-align: center;
      margin-bottom: 24px;
    }

    .ws-icon-circle {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      background: #e6f7ef;
      color: #10955d;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 24px;
      margin: 0 auto 16px auto;
    }

    .ws-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin-bottom: 16px;
    }

    .ws-card-btn {
      display: flex;
      align-items: center;
      padding: 14px 16px;
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
      box-shadow: 0 4px 12px rgba(16, 149, 93, 0.1);
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
      margin-right: 12px;
      flex-shrink: 0;
    }

    .ws-body {
      flex: 1;
      overflow: hidden;
    }

    .ws-name {
      font-size: 14px;
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


    /* Responsive */
    @media (max-width: 1024px) {
      .login-container {
        grid-template-columns: 1fr;
      }
      .hero-panel {
        display: none;
      }
      .mobile-logo {
        display: flex;
      }
      .auth-panel {
        padding: 32px 16px;
      }
      .auth-card {
        padding: 30px 24px;
      }
    }
  `],
  template: `
    <div class="login-container">
      <!-- Left Side: Enterprise Showcase -->
      <section class="hero-panel">
        <div class="ambient-glow-1"></div>
        <div class="ambient-glow-2"></div>
        <div class="grid-overlay"></div>

        <div class="hero-inner">
          <div class="dark">
            <div class="hero-brand-wrap" data-testid="login-brand">
              <erp-brand-logo size="lg"></erp-brand-logo>
            </div>
          </div>

          <div style="margin: 40px 0;">
            <div class="status-chip">
              <span class="pulse-dot"></span>
              <span>Unified Enterprise Operating System</span>
            </div>

            <h1 class="hero-heading">
              Sovereign, Intelligent <span class="gradient-text">Enterprise Control</span>
            </h1>

            <p class="hero-sub">
              Dual-entry financial ledger, modular real estate lifecycle, CRM pipeline, and multi-tenant security designed for high-scale operations.
            </p>

            <!-- Floating Live Telemetry Card -->
            <div class="glass-card">
              <div class="glass-head">
                <div class="glass-title-group">
                  <span nz-icon nzType="dashboard" nzTheme="outline" style="color:#34d399;"></span>
                  <span>System Architecture Status</span>
                </div>
                <span class="glass-live">● Operational</span>
              </div>

              <div class="metrics-row">
                <div class="metric-col">
                  <span class="metric-lbl">Ledger Engine</span>
                  <span class="metric-kpi">Balanced</span>
                  <span class="metric-status"><span nz-icon nzType="check-circle" nzTheme="fill"></span> Real-time</span>
                </div>
                <div class="metric-col">
                  <span class="metric-lbl">Multi-Tenancy</span>
                  <span class="metric-kpi">Org Scoped</span>
                  <span class="metric-status"><span nz-icon nzType="safety" nzTheme="outline"></span> AES-256</span>
                </div>
                <div class="metric-col">
                  <span class="metric-lbl">Uptime SLA</span>
                  <span class="metric-kpi">99.99%</span>
                  <span class="metric-status"><span nz-icon nzType="thunderbolt" nzTheme="fill"></span> Low latency</span>
                </div>
              </div>
            </div>
          </div>

          <div class="hero-bottom-text">
            Enterprise Cloud Infrastructure · Zero-Data-Leakage Design · ISO 27001 Ready
          </div>
        </div>
      </section>

      <!-- Right Side: Authentication Box -->
      <main class="auth-panel">
        <div class="auth-card">
          <!-- Mobile logo (if hero is hidden on small screens) -->
          <div class="mobile-logo">
            <erp-brand-logo size="md"></erp-brand-logo>
          </div>

          @if (!workspaces()) {
            <div class="auth-header">
              <h2 class="auth-title">{{ 'login.title' | translate: 'Sign in' }}</h2>
              <p class="auth-desc">{{ 'login.sub' | translate: 'Enter your work email and password to access your workspace.' }}</p>
            </div>

            @if (error()) {
              <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon class="auth-alert" data-testid="login-error"></nz-alert>
            }

            <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()" class="auth-form">
              <nz-form-item>
                <nz-form-label nzFor="email" nzRequired>{{ 'common.email' | translate: 'Email' }}</nz-form-label>
                <nz-form-control nzErrorTip="Enter your work email address">
                  <nz-input-group [nzPrefix]="emailPrefix" class="auth-input-group">
                    <input nz-input id="email" type="email" formControlName="email" autocomplete="username" placeholder="name@company.com" data-testid="login-email" />
                  </nz-input-group>
                  <ng-template #emailPrefix><span nz-icon nzType="mail" class="field-icon"></span></ng-template>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzFor="password" nzRequired>{{ 'common.password' | translate: 'Password' }}</nz-form-label>
                <nz-form-control nzErrorTip="Enter your password">
                  <nz-input-group [nzPrefix]="pwdPrefix" [nzSuffix]="pwdSuffix" class="auth-input-group">
                    <input nz-input id="password" [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" placeholder="••••••••••••" data-testid="login-password" />
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
                <nz-form-item>
                  <nz-form-label nzFor="code" nzRequired>{{ 'login.code' | translate: 'Verification code' }}</nz-form-label>
                  <nz-form-control nzExtra="Enter the 6-digit code from your authenticator app, or a recovery code.">
                    <nz-input-group [nzPrefix]="codePrefix" class="auth-input-group">
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

              <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" [disabled]="busy() || retryIn() > 0" class="sign-in-btn" data-testid="login-submit">
                @if (retryIn() > 0) {
                  <span>Try again in {{ retryIn() }}s</span>
                } @else {
                  <span>{{ 'login.title' | translate: 'Sign in' }}</span>
                  <span nz-icon nzType="arrow-right"></span>
                }
              </button>
            </form>


          } @else {
            <!-- Multi-Workspace Picker -->
            <div class="ws-header">
              <div class="ws-icon-circle">
                <span nz-icon nzType="appstore" nzTheme="outline"></span>
              </div>
              <h2 class="auth-title">{{ 'login.chooseWorkspace' | translate: 'Choose a workspace' }}</h2>
              <p class="auth-desc">Your credentials belong to several enterprise workspaces. Choose which one to enter:</p>
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

            <button nz-button nzType="text" nzBlock (click)="workspaces.set(null)" style="color:#64748b; font-size:13px;">
              ← Back to login
            </button>
          }


        </div>
      </main>
    </div>`,
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
