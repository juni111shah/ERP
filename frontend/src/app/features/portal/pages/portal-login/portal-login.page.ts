import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AUTH_STYLES } from '@features/auth/auth-layout';
import { PortalService } from '../../portal.service';

/** Sign-in for outside customers and suppliers: workspace + email, then the 6-digit code that was emailed. */
@Component({
  selector: 'erp-portal-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, ReactiveFormsModule, NzCheckboxModule, NzFormModule, NzInputModule, NzButtonModule, NzAlertModule],
  styles: [AUTH_STYLES],
  templateUrl: './portal-login.page.html',
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
