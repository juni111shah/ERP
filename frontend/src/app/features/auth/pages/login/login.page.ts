import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService, LoginResponse } from '@core/auth/auth.service';
import { safeReturnUrl } from '@core/guards/access.guards';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { Workspace } from '@core/session/session.store';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { TwoFactorChallenge, retryAfterSeconds, signInFailureText, startCountdown, twoFactorChallengeOf } from '../../auth-sign-in';
import { TwoFactorStepComponent } from '../../components/two-factor-step/two-factor-step.component';

/** Sign-in: email and password, then two-factor when needed, then a workspace choice when the person has several. */
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
    BrandLogoComponent,
    TwoFactorStepComponent,
  ],
  styleUrl: './login.page.scss',
  templateUrl: './login.page.html',
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
  /** Two-factor step after a correct password: first-sign-in setup, or a code for a returning user. */
  readonly challenge = signal<TwoFactorChallenge | null>(null);
  private selectionToken = '';

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => c.updateValueAndValidity()); return; }
    const { email, password, rememberMe, code } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(email, password, rememberMe, code || undefined).subscribe({
      next: (r) => { this.busy.set(false); this.afterPassword(r); },
      error: (e) => { this.busy.set(false); this.fail(e); },
    });
  }

  afterTwoFactor(r: LoginResponse): void {
    this.challenge.set(null);
    this.form.controls.password.reset('');
    this.afterSignIn(r);
  }

  /** "Back to sign in" from the two-factor step (or after it expired): start again from email and password. */
  restart(): void {
    this.challenge.set(null);
    this.error.set(null);
    this.form.controls.password.reset('');
  }

  pick(w: Workspace): void {
    this.busy.set(true);
    this.auth.selectWorkspace(this.selectionToken, w.id).subscribe({
      next: () => this.done(),
      error: (e) => { this.busy.set(false); this.workspaces.set(null); this.fail(e); },
    });
  }

  /** A correct password: on to two-factor when the answer asks for it, else into the session. */
  private afterPassword(r: LoginResponse): void {
    const challenge = twoFactorChallengeOf(r);
    if (challenge) { this.challenge.set(challenge); return; }
    if (r.requiresTwoFactor) { this.needsCode.set(true); return; } // older API: code typed into the login form
    this.afterSignIn(r);
  }

  private afterSignIn(r: LoginResponse): void {
    if (r.requiresWorkspaceSelection) { this.selectionToken = r.selectionToken!; this.workspaces.set(r.workspaces); }
    else this.done();
  }

  private done(): void {
    void this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
  }

  /** Clears the secret just typed and says what went wrong; a rate limit also counts down until the next try. */
  private fail(e: unknown): void {
    const p = asProblem(e);
    if (!this.needsCode()) this.form.controls.password.reset('');
    else this.form.controls.code.reset('');
    if (p?.status === 429) {
      this.error.set('Too many attempts. Please wait before trying again.');
      startCountdown(this.retryIn, retryAfterSeconds(p));
    } else {
      this.error.set(signInFailureText(p));
    }
  }
}
