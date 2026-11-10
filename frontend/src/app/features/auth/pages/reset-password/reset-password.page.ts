import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService } from '@core/auth/auth.service';
import { asProblem } from '@core/http/problem-details';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { revealErrors } from '@shared/forms/form-validation';
import { AUTH_STYLES, PASSWORD_HINT, PASSWORD_RULE } from '../../auth-layout';

/** Sets a new password from the link in the reset email. */
@Component({
  selector: 'erp-reset-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule, BrandLogoComponent],
  styles: [AUTH_STYLES],
  templateUrl: './reset-password.page.html',
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
    if (this.form.invalid) { revealErrors(this.form); return; }
    this.busy.set(true);
    this.auth.resetPassword(this.token(), this.form.getRawValue().password).subscribe({
      next: () => this.done.set(true),
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'This reset link is invalid or has expired.'); },
    });
  }
}
