import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AUTH_STYLES } from '../auth-layout';
import { PortalService } from './portal.service';

/** Tenant sign-in: workspace + email, then the 6-digit code that was emailed. */
@Component({
  selector: 'erp-portal-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule],
  styles: [AUTH_STYLES],
  template: `
    <div class="card">
      <div class="brand"><span class="brand-mark">T</span> Tenant portal</div>
      <h1>Sign in</h1>
      <p class="sub">{{ sent() ? 'Enter the code we emailed you.' : 'We will email you a one-time code.' }}</p>
      @if (error()) { <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon style="margin-bottom:16px" data-testid="portal-error"></nz-alert> }
      <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
        <nz-form-item><nz-form-label nzRequired>Workspace code</nz-form-label><nz-form-control nzErrorTip="Required"><input nz-input formControlName="workspace" data-testid="portal-workspace" /></nz-form-control></nz-form-item>
        <nz-form-item><nz-form-label nzRequired>Email</nz-form-label><nz-form-control nzErrorTip="Enter your email"><input nz-input type="email" formControlName="email" data-testid="portal-email" /></nz-form-control></nz-form-item>
        @if (sent()) {
          <nz-form-item><nz-form-label nzRequired>Code</nz-form-label><nz-form-control nzErrorTip="Enter the 6-digit code"><input nz-input formControlName="code" maxlength="6" inputmode="numeric" data-testid="portal-code" /></nz-form-control></nz-form-item>
        }
        <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" data-testid="portal-submit">{{ sent() ? 'Sign in' : 'Email me a code' }}</button>
      </form>
    </div>`,
})
export class PortalLoginPage {
  private readonly portal = inject(PortalService);
  private readonly router = inject(Router);
  readonly form = inject(FormBuilder).nonNullable.group({ workspace: ['', Validators.required], email: ['', [Validators.required, Validators.email]], code: [''] });
  readonly sent = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    const { workspace, email, code } = this.form.getRawValue();
    if (this.form.controls.workspace.invalid || this.form.controls.email.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    this.error.set(null);
    if (!this.sent()) {
      this.portal.requestCode(workspace, email).subscribe({
        next: () => { this.busy.set(false); this.sent.set(true); },
        error: () => { this.busy.set(false); this.error.set('Could not send a code. Try again in a minute.'); },
      });
      return;
    }
    this.portal.verify(workspace, email, code).subscribe({
      next: () => { this.busy.set(false); void this.router.navigateByUrl('/portal'); },
      error: () => { this.busy.set(false); this.error.set('The code is invalid or has expired.'); },
    });
  }
}
