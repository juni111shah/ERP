import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService } from '../core/auth/auth.service';
import { AUTH_STYLES } from './auth-layout';

@Component({
  selector: 'erp-forgot-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule],
  styles: [AUTH_STYLES],
  template: `
    <div class="card">
      <div class="brand"><span class="brand-mark">E</span> ERP</div>
      <h1>Reset your password</h1>
      @if (sent()) {
        <nz-alert nzType="success" nzShowIcon data-testid="forgot-sent"
          nzMessage="If an account exists for that email, we have sent a reset link. It expires in 30 minutes."></nz-alert>
      } @else {
        <p class="sub">Enter your work email and we will send you a link.</p>
        <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
          <nz-form-item>
            <nz-form-label nzFor="email" nzRequired>Email</nz-form-label>
            <nz-form-control nzErrorTip="Enter a valid email address"><input nz-input id="email" type="email" formControlName="email" data-testid="forgot-email" /></nz-form-control>
          </nz-form-item>
          <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" data-testid="forgot-submit">Send reset link</button>
        </form>
      }
      <p style="margin-top:16px"><a routerLink="/auth/login">Back to sign in</a></p>
    </div>`,
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);
  readonly form = inject(FormBuilder).nonNullable.group({ email: ['', [Validators.required, Validators.email]] });
  readonly busy = signal(false);
  readonly sent = signal(false);

  submit(): void {
    if (this.form.invalid) { this.form.controls.email.markAsDirty(); this.form.controls.email.updateValueAndValidity(); return; }
    this.busy.set(true);
    // Same message whether or not the account exists (no user enumeration).
    this.auth.forgotPassword(this.form.getRawValue().email).subscribe({ next: () => this.sent.set(true), error: () => this.sent.set(true) });
  }
}
