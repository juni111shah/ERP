import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService } from '@core/auth/auth.service';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { revealErrors } from '@shared/forms/form-validation';
import { AUTH_STYLES } from '../../auth-layout';

/** Asks for an email and sends a reset link; the answer is the same whether or not the account exists. */
@Component({
  selector: 'erp-forgot-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule, BrandLogoComponent],
  styles: [AUTH_STYLES],
  templateUrl: './forgot-password.page.html',
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);
  readonly form = inject(FormBuilder).nonNullable.group({ email: ['', [Validators.required, Validators.email]] });
  readonly busy = signal(false);
  readonly sent = signal(false);

  submit(): void {
    if (this.form.invalid) { revealErrors(this.form); return; }
    this.busy.set(true);
    // Same message whether or not the account exists (no user enumeration).
    this.auth.forgotPassword(this.form.getRawValue().email).subscribe({ next: () => this.sent.set(true), error: () => this.sent.set(true) });
  }
}
