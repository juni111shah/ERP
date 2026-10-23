import { TranslatePipe } from '../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService } from '../core/auth/auth.service';
import { safeReturnUrl } from '../core/guards/access.guards';
import { asProblem } from '../core/http/problem-details';
import { Workspace } from '../core/session/session.store';
import { AUTH_STYLES } from './auth-layout';

@Component({
  selector: 'erp-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzCheckboxModule, NzAlertModule],
  styles: [AUTH_STYLES],
  template: `
    <div class="card">
      <div class="brand"><span class="brand-mark">E</span> ERP</div>
      @if (!workspaces()) {
        <h1>{{ 'login.title' | translate: 'Sign in' }}</h1>
        <p class="sub">{{ 'login.sub' | translate: 'Use your work email.' }}</p>
        @if (error()) { <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon style="margin-bottom:16px" data-testid="login-error"></nz-alert> }
        <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
          <nz-form-item>
            <nz-form-label nzFor="email" nzRequired>{{ 'common.email' | translate: 'Email' }}</nz-form-label>
            <nz-form-control nzErrorTip="Enter your email address">
              <input nz-input id="email" type="email" formControlName="email" autocomplete="username" data-testid="login-email" />
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label nzFor="password" nzRequired>{{ 'common.password' | translate: 'Password' }}</nz-form-label>
            <nz-form-control nzErrorTip="Enter your password">
              <input nz-input id="password" type="password" formControlName="password" autocomplete="current-password" data-testid="login-password" />
            </nz-form-control>
          </nz-form-item>
          @if (needsCode()) {
            <nz-form-item>
              <nz-form-label nzFor="code" nzRequired>{{ 'login.code' | translate: 'Verification code' }}</nz-form-label>
              <nz-form-control nzExtra="From your authenticator app, or a recovery code.">
                <input nz-input id="code" formControlName="code" autocomplete="one-time-code" inputmode="numeric" data-testid="login-code" />
              </nz-form-control>
            </nz-form-item>
          }
          <div class="row">
            <label nz-checkbox formControlName="rememberMe" data-testid="login-remember">{{ 'login.remember' | translate: 'Keep me signed in' }}</label>
            <a routerLink="/auth/forgot-password" data-testid="login-forgot">{{ 'login.forgot' | translate: 'Forgot password?' }}</a>
          </div>
          <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" [disabled]="busy() || retryIn() > 0" data-testid="login-submit">
            {{ retryIn() > 0 ? 'Try again in ' + retryIn() + 's' : ('login.title' | translate: 'Sign in') }}
          </button>
        </form>
      } @else {
        <h1>{{ 'login.chooseWorkspace' | translate: 'Choose a workspace' }}</h1>
        <p class="sub">Your account belongs to several workspaces.</p>
        <div class="ws" data-testid="workspace-picker">
          @for (w of workspaces(); track w.id) {
            <button nz-button nzBlock (click)="pick(w)" [disabled]="busy()" [attr.data-testid]="'workspace-' + w.code">
              {{ w.name }}<small>{{ w.industry }}</small>
            </button>
          }
        </div>
      }
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
