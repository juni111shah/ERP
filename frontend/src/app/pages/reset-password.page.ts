import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService } from '../core/auth/auth.service';
import { asProblem } from '../core/http/problem-details';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { AUTH_STYLES, PASSWORD_HINT, PASSWORD_RULE } from './auth-layout';

@Component({
  selector: 'erp-reset-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule, BrandLogoComponent],
  styles: [AUTH_STYLES],
  template: `
    <div class="card">
      <div class="brand"><erp-brand-logo size="lg"></erp-brand-logo></div>
      <h1>Choose a new password</h1>
      @if (done()) {
        <nz-alert nzType="success" nzShowIcon nzMessage="Your password has been changed and other devices were signed out." data-testid="reset-done"></nz-alert>
        <p style="margin-top:16px"><a routerLink="/auth/login">Sign in</a></p>
      } @else {
        <p class="sub">{{ hint }}</p>
        @if (error()) { <nz-alert nzType="error" nzShowIcon [nzMessage]="error()!" style="margin-bottom:16px" data-testid="reset-error"></nz-alert> }
        <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
          <nz-form-item>
            <nz-form-label nzFor="pw" nzRequired>New password</nz-form-label>
            <nz-form-control [nzErrorTip]="hint"><input nz-input id="pw" type="password" formControlName="password" autocomplete="new-password" data-testid="reset-password" /></nz-form-control>
          </nz-form-item>
          <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" data-testid="reset-submit">Change password</button>
        </form>
      }
    </div>`,
})
export class ResetPasswordPage {
  readonly token = input<string>('');
  readonly hint = PASSWORD_HINT;
  private readonly auth = inject(AuthService);
  readonly form = inject(FormBuilder).nonNullable.group({ password: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]] });
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) { this.form.controls.password.markAsDirty(); this.form.controls.password.updateValueAndValidity(); return; }
    this.busy.set(true);
    this.auth.resetPassword(this.token(), this.form.getRawValue().password).subscribe({
      next: () => this.done.set(true),
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'This reset link is invalid or has expired.'); },
    });
  }
}
