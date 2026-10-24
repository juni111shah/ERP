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
      height: 100vh;
      max-height: 100vh;
      width: 100%;
      overflow: hidden;
      background: linear-gradient(135deg, #f0fdf4 0%, #f8fafc 40%, #e6f7ef 100%);
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      color: #0f172a;
    }

    .login-root {
      display: grid;
      grid-template-columns: 43% 57%;
      height: 100vh;
      max-height: 100vh;
      width: 100%;
      background: linear-gradient(135deg, #f0fdf4 0%, #f8fafc 40%, #e6f7ef 100%);
      position: relative;
      overflow: hidden;
    }

    /* Left Side: Authentication Panel */
    .login-left-pane {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      padding: 36px 64px 28px 64px;
      background: transparent;
      position: relative;
      z-index: 10;
      height: 100vh;
      box-sizing: border-box;
      overflow-y: auto;
    }

    .brand-top {
      display: inline-flex;
      align-items: center;
      margin-bottom: 12px;
      flex-shrink: 0;
    }

    .login-main-content {
      width: 100%;
      max-width: 420px;
      margin: auto 0;
      padding: 8px 0;
    }

    .login-heading-group {
      margin-bottom: 24px;
    }

    .login-title {
      font-size: 36px;
      font-weight: 800;
      color: #0f172a;
      letter-spacing: -0.025em;
      line-height: 1.15;
      margin: 0 0 8px 0;
    }

    .login-subtitle {
      font-size: 14px;
      color: #64748b;
      margin: 0;
      line-height: 1.45;
    }

    .auth-alert {
      margin-bottom: 18px;
      border-radius: 8px;
    }

    .auth-form {
      display: flex;
      flex-direction: column;
    }

    .form-item {
      margin-bottom: 16px;
    }

    .form-item ::ng-deep .ant-form-item-label > label {
      font-size: 13px;
      font-weight: 600;
      color: #334155;
      height: auto;
      margin-bottom: 4px;
    }

    .custom-input-group {
      border: 1.5px solid rgba(16, 149, 93, 0.2);
      border-radius: 10px;
      background: rgba(255, 255, 255, 0.9);
      backdrop-filter: blur(10px);
      box-shadow: 0 2px 6px rgba(16, 149, 93, 0.04);
      transition: all 0.2s ease;
      overflow: hidden;
    }

    .custom-input-group:hover {
      border-color: #10955d;
      background: #ffffff;
      box-shadow: 0 4px 12px rgba(16, 149, 93, 0.08);
    }

    .custom-input-group:focus-within {
      border-color: #10955d;
      background: #ffffff;
      box-shadow: 0 0 0 3.5px rgba(16, 149, 93, 0.14);
    }

    .custom-input-group input {
      height: 42px;
      font-size: 14.5px;
      border: none;
      background: transparent;
      box-shadow: none !important;
      color: #0f172a;
    }

    .custom-input-group input::placeholder {
      color: #94a3b8;
    }

    .custom-input-group input:-webkit-autofill,
    .custom-input-group input:-webkit-autofill:hover,
    .custom-input-group input:-webkit-autofill:focus {
      -webkit-box-shadow: 0 0 0 1000px #ffffff inset !important;
      -webkit-text-fill-color: #0f172a !important;
    }

    .field-icon {
      color: #94a3b8;
      font-size: 16px;
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
      margin: 4px 0 22px 0;
      font-size: 13px;
    }

    .aux-row ::ng-deep .ant-checkbox-wrapper {
      color: #475569;
      font-size: 13px;
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
      margin-top: 2px;
    }

    .login-btn {
      height: 46px;
      min-width: 160px;
      padding: 0 32px;
      font-size: 13.5px;
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

    .brand-footer-note {
      font-size: 11.5px;
      color: #94a3b8;
      letter-spacing: 0.02em;
      padding-top: 14px;
      flex-shrink: 0;
    }

    /* Multi-Workspace Picker */
    .ws-header {
      text-align: left;
      margin-bottom: 20px;
    }

    .ws-icon-circle {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      background: #e6f7ef;
      color: #10955d;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 20px;
      margin-bottom: 12px;
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
      padding: 12px 16px;
      background: rgba(255, 255, 255, 0.9);
      backdrop-filter: blur(8px);
      border: 1.5px solid rgba(16, 149, 93, 0.16);
      border-radius: 10px;
      cursor: pointer;
      text-align: left;
      transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
      width: 100%;
    }

    .ws-card-btn:hover:not(:disabled) {
      border-color: #10955d;
      background: #ffffff;
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(16, 149, 93, 0.12);
    }

    .ws-avatar {
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: #10955d;
      color: #ffffff;
      font-weight: 700;
      font-size: 14px;
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
      font-size: 11.5px;
      color: #64748b;
      margin-top: 1px;
    }

    .ws-caret {
      color: #94a3b8;
      font-size: 13px;
    }

    /* Right Side: Seamless Illustration & App Story */
    .login-right-pane {
      position: relative;
      background: transparent;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: space-between;
      padding: 40px 52px 32px 52px;
      height: 100vh;
      box-sizing: border-box;
      overflow: hidden;
      z-index: 1;
    }

    .organic-blob-1 {
      position: absolute;
      top: -15%;
      right: -10%;
      width: 600px;
      height: 600px;
      border-radius: 46% 54% 65% 35% / 40% 45% 55% 60%;
      background: radial-gradient(circle, rgba(16, 149, 93, 0.13) 0%, rgba(52, 188, 134, 0.04) 50%, transparent 75%);
      filter: blur(50px);
      pointer-events: none;
      z-index: 1;
    }

    .organic-blob-2 {
      position: absolute;
      bottom: -15%;
      left: 5%;
      width: 480px;
      height: 480px;
      border-radius: 58% 42% 48% 52% / 55% 60% 40% 45%;
      background: radial-gradient(circle, rgba(16, 185, 129, 0.1) 0%, rgba(16, 149, 93, 0.02) 55%, transparent 75%);
      filter: blur(45px);
      pointer-events: none;
      z-index: 1;
    }

    .floating-orb {
      position: absolute;
      top: 12%;
      right: 6%;
      width: 18px;
      height: 18px;
      border-radius: 50%;
      background: linear-gradient(135deg, #34bc86, #10955d);
      box-shadow: 0 4px 12px rgba(16, 149, 93, 0.35);
      animation: floatOrb 5s ease-in-out infinite alternate;
      z-index: 2;
    }

    /* TOP STORY CARD (Above Illustration) */
    .top-story-card {
      position: relative;
      z-index: 10;
      width: 100%;
      max-width: 660px;
      margin-top: 4px;
      text-align: left;
    }

    .shimmer-chip {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 6px 16px;
      border-radius: 9999px;
      background: linear-gradient(90deg, rgba(16, 149, 93, 0.1) 0%, rgba(52, 211, 153, 0.28) 50%, rgba(16, 149, 93, 0.1) 100%);
      background-size: 200% 100%;
      border: 1px solid rgba(16, 149, 93, 0.3);
      color: #0d7b4c;
      font-size: 11.5px;
      font-weight: 700;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      margin-bottom: 10px;
      animation: badgeShimmer 4s infinite linear;
      box-shadow: 0 2px 8px rgba(16, 149, 93, 0.08);
    }

    .chip-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: #10955d;
      box-shadow: 0 0 8px #10955d;
      animation: pulseDot 2s infinite ease-in-out;
    }

    .shimmer-headline {
      font-size: 23px;
      font-weight: 800;
      line-height: 1.3;
      letter-spacing: -0.02em;
      margin: 0 0 6px 0;
      background: linear-gradient(135deg, #0f172a 0%, #10955d 40%, #047857 75%, #0f172a 100%);
      background-size: 250% auto;
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      animation: headlineShimmer 6s ease-in-out infinite alternate;
    }

    .top-subtext {
      font-size: 13.5px;
      color: #475569;
      line-height: 1.5;
      margin: 0;
      max-width: 580px;
    }

    /* CENTER ILLUSTRATION */
    .illustration-wrapper {
      position: relative;
      z-index: 5;
      width: 100%;
      max-width: 660px;
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 0;
      padding: 10px 0;
    }

    .animated-svg-img {
      width: 100%;
      height: 100%;
      max-height: 48vh;
      object-fit: contain;
      filter: drop-shadow(0 18px 36px rgba(16, 149, 93, 0.08));
    }

    /* BOTTOM FEATURE PILLS (Below Illustration) */
    .bottom-story-card {
      position: relative;
      z-index: 10;
      width: 100%;
      max-width: 660px;
      margin-bottom: 8px;
    }

    .feature-pills-row {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      align-items: center;
    }

    .shimmer-pill {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      padding: 6px 14px;
      border-radius: 9999px;
      background: rgba(255, 255, 255, 0.85);
      backdrop-filter: blur(8px);
      border: 1px solid rgba(16, 149, 93, 0.22);
      font-size: 12px;
      font-weight: 600;
      color: #065f46;
      box-shadow: 0 2px 8px rgba(16, 149, 93, 0.06);
      transition: all 0.2s ease;
    }

    .shimmer-pill:hover {
      transform: translateY(-1px);
      border-color: #10955d;
      box-shadow: 0 4px 12px rgba(16, 149, 93, 0.12);
    }

    .shimmer-pill span[nz-icon] {
      color: #10955d;
      font-size: 13px;
    }

    /* Keyframes */
    @keyframes badgeShimmer {
      0% { background-position: -200% 0; }
      100% { background-position: 200% 0; }
    }

    @keyframes headlineShimmer {
      0% { background-position: 0% 50%; }
      50% { background-position: 100% 50%; }
      100% { background-position: 0% 50%; }
    }

    @keyframes pulseDot {
      0%, 100% { transform: scale(1); opacity: 1; }
      50% { transform: scale(1.3); opacity: 0.7; }
    }

    @keyframes floatOrb {
      0% { transform: translateY(0px); }
      100% { transform: translateY(-10px); }
    }

    /* Responsive */
    @media (max-width: 1080px) {
      :host {
        height: auto;
        max-height: none;
        overflow-y: auto;
      }
      .login-root {
        grid-template-columns: 1fr;
        height: auto;
        max-height: none;
        overflow-y: auto;
      }
      .login-left-pane {
        padding: 36px 28px;
        height: auto;
        min-height: 100vh;
      }
      .login-main-content {
        margin: 20px auto;
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
              <h2 class="login-title" style="font-size: 24px;">{{ 'login.chooseWorkspace' | translate: 'Choose a workspace' }}</h2>
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

            <button nz-button nzType="text" (click)="workspaces.set(null)" style="color:#64748b; font-size:13px; padding-left: 0;">
              ← Back to login
            </button>
          }
        </div>

        <div class="brand-footer-note">
          Enterprise Cloud Infrastructure · Sovereign & Multi-Tenant
        </div>
      </section>

      <!-- Right Panel: Seamless Brand Themed Story & Illustration -->
      <section class="login-right-pane">
        <div class="organic-blob-1"></div>
        <div class="organic-blob-2"></div>
        <div class="floating-orb"></div>

        <!-- TOP TEXT: Shimmer Headline & Value Proposition -->
        <div class="top-story-card">
          <div class="shimmer-chip">
            <span class="chip-dot"></span>
            <span>Enterprise Resource Planning · 360 Suite</span>
          </div>
          <h2 class="shimmer-headline">
            Intelligent Operations, Real-Time Financials & Sovereign Multi-Tenant Scale
          </h2>
          <p class="top-subtext">
            Unify dual-entry ledgers, dynamic billing, warehouse logistics, and point-of-sale in one high-performance enterprise suite.
          </p>
        </div>

        <!-- CENTER: Animated SVG Illustration -->
        <div class="illustration-wrapper">
          <img
            src="assets/data-dashboard.svg"
            alt="Business Analytics Dashboard"
            class="animated-svg-img" />
        </div>

        <!-- BOTTOM TEXT: Live Feature Highlights & Shimmer Badges -->
        <div class="bottom-story-card">
          <div class="feature-pills-row">
            <div class="shimmer-pill">
              <span nz-icon nzType="check-circle" nzTheme="fill"></span>
              <span>Real-Time Balanced Ledger</span>
            </div>
            <div class="shimmer-pill">
              <span nz-icon nzType="safety-certificate" nzTheme="fill"></span>
              <span>Org-Scoped Security</span>
            </div>
            <div class="shimmer-pill">
              <span nz-icon nzType="thunderbolt" nzTheme="fill"></span>
              <span>Sub-Second Response</span>
            </div>
            <div class="shimmer-pill">
              <span nz-icon nzType="audit" nzTheme="outline"></span>
              <span>Immutable Audit Trails</span>
            </div>
          </div>
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
