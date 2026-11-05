import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AUTH_STYLES } from '../auth-layout';
import { PortalService } from './portal.service';

/** Sign-in for outside customers and suppliers: workspace + email, then the 6-digit code that was emailed. */
@Component({
  selector: 'erp-portal-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, ReactiveFormsModule, NzCheckboxModule, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule],
  styles: [AUTH_STYLES],
  template: `
    <div class="card">
      <div class="brand"><span class="brand-mark">P</span><span style="margin-inline-start:8px">{{ 'portal.title' | translate: 'Customer and supplier portal' }}</span></div>
      <h1>{{ 'portal.signIn' | translate: 'Sign in' }}</h1>
      <p class="sub">{{ sent() ? 'If this email is on file with us, a 6-digit code is on its way. It can take a minute, so check your spam folder too.' : 'No password needed. We email you a one-time code.' }}</p>
      @if (error()) { <nz-alert nzType="error" [nzMessage]="error()!" nzShowIcon style="margin-bottom:16px" data-testid="portal-error"></nz-alert> }
      <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="submit()">
        <nz-form-item><nz-form-label nzRequired>{{ 'portal.workspace' | translate: 'Workspace code' }}</nz-form-label><nz-form-control nzErrorTip="Enter the workspace code" nzExtra="The company you deal with gives you this code."><input nz-input formControlName="workspace" autocomplete="organization" data-testid="portal-workspace" /></nz-form-control></nz-form-item>
        <label nz-checkbox formControlName="vendor" style="margin-bottom:12px" data-testid="portal-vendor">I am a vendor / supplier</label>
        <nz-form-item><nz-form-label nzRequired>{{ 'common.email' | translate: 'Email' }}</nz-form-label><nz-form-control nzErrorTip="Enter your email"><input nz-input type="email" formControlName="email" autocomplete="email" data-testid="portal-email" /></nz-form-control></nz-form-item>
        @if (sent()) {
          <nz-form-item><nz-form-label nzRequired>Code</nz-form-label><nz-form-control nzErrorTip="Enter the 6-digit code"><input nz-input formControlName="code" maxlength="6" inputmode="numeric" autocomplete="one-time-code" data-testid="portal-code" /></nz-form-control></nz-form-item>
        }
        <button nz-button nzType="primary" nzBlock [nzLoading]="busy()" data-testid="portal-submit">{{ sent() ? ('portal.signIn' | translate: 'Sign in') : ('portal.emailCode' | translate: 'Email me a code') }}</button>
      </form>
      @if (sent()) {
        <div style="display:flex;justify-content:space-between;gap:8px;margin-top:12px">
          <button nz-button nzType="link" type="button" [disabled]="busy()" (click)="resend()" data-testid="portal-resend">Send a new code</button>
          <button nz-button nzType="link" type="button" [disabled]="busy()" (click)="restart()" data-testid="portal-restart">Use another email</button>
        </div>
      }
    </div>`,
})
export class PortalLoginPage {
  private readonly portal = inject(PortalService);
  private readonly router = inject(Router);
  readonly form = inject(FormBuilder).nonNullable.group({ workspace: ['', Validators.required], email: ['', [Validators.required, Validators.email]], code: [''], vendor: [false] });
  readonly sent = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    const { workspace, email, code, vendor } = this.form.getRawValue();
    const kind = vendor ? 'vendor' : undefined;
    if (this.form.controls.workspace.invalid || this.form.controls.email.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    this.error.set(null);
    if (!this.sent()) {
      this.portal.requestCode(workspace, email, kind).subscribe({
        next: () => { this.busy.set(false); this.sent.set(true); },
        error: () => { this.busy.set(false); this.error.set('Could not send a code. Try again in a minute.'); },
      });
      return;
    }
    if (!code.trim()) { this.busy.set(false); this.error.set('Enter the 6-digit code from the email.'); return; }
    this.portal.verify(workspace, email, code.trim(), kind).subscribe({
      next: (r) => { this.busy.set(false); void this.router.navigateByUrl(r.kind === 'vendor' ? '/portal/vendor' : '/portal'); },
      error: () => { this.busy.set(false); this.error.set('The code is invalid or has expired. Check it, or send a new code.'); },
    });
  }

  resend(): void {
    const { workspace, email, vendor } = this.form.getRawValue();
    this.busy.set(true); this.error.set(null);
    this.portal.requestCode(workspace, email, vendor ? 'vendor' : undefined).subscribe({
      next: () => { this.busy.set(false); this.form.controls.code.setValue(''); },
      error: () => { this.busy.set(false); this.error.set('Could not send a code. Try again in a minute.'); },
    });
  }

  restart(): void { this.sent.set(false); this.error.set(null); this.form.controls.code.setValue(''); }
}
