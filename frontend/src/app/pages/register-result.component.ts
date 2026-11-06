import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SignupResult } from '../core/api/platform.api';

/** What happens next after a registration: the same panel after the form and after the email link. */
@Component({
  selector: 'erp-register-result',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule],
  styles: [`
    :host { display: block; }
    .panel { text-align: center; padding: 8px 0; }
    .icon { width: 56px; height: 56px; margin: 0 auto 14px; display: grid; place-items: center; border-radius: 50%; font-size: 26px; background: var(--erp-color-primary-subtle); color: var(--erp-color-primary-text); }
    .panel[data-state='pendingEmail'] .icon, .panel[data-state='pendingReview'] .icon { background: var(--erp-color-info-bg); color: var(--erp-color-info-solid); }
    h2 { margin: 0 0 8px; font-size: 20px; font-weight: 700; color: var(--erp-color-text-primary); }
    p { margin: 0 auto 12px; max-width: 420px; color: var(--erp-color-text-muted); line-height: 1.5; }
    .code { display: inline-block; margin: 4px 0 16px; padding: 8px 16px; border-radius: 8px; background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-subtle); }
    .code small { display: block; color: var(--erp-color-text-muted); font-size: 12px; }
    .code strong { font-family: monospace; font-size: 16px; color: var(--erp-color-text-primary); }
  `],
  template: `
    @let r = result();
    <div class="panel" [attr.data-state]="r.status" [attr.data-testid]="'register-result-' + r.status">
      <div class="icon" aria-hidden="true"><span nz-icon [nzType]="icon()"></span></div>
      <h2>{{ heading() }}</h2>
      <p>{{ r.message }}</p>
      @if (r.status === 'provisioned') {
        @if (r.signInKey) { <div class="code"><small>Workspace code</small><strong data-testid="register-result-code">{{ r.signInKey }}</strong></div> }
        <div><a nz-button nzType="primary" routerLink="/auth/login" data-testid="register-result-signin">Sign in</a></div>
      } @else {
        @if (supportEmail(); as mail) { <p>Questions? Write to <a [href]="'mailto:' + mail">{{ mail }}</a>.</p> }
        <a routerLink="/auth/login">Back to sign in</a>
      }
    </div>`,
})
export class RegisterResultComponent {
  readonly result = input.required<SignupResult>();
  readonly supportEmail = input<string | null>(null);
  readonly icon = computed(() => (this.result().status === 'provisioned' ? 'check-circle' : this.result().status === 'pendingEmail' ? 'mail' : 'clock-circle'));
  readonly heading = computed(() => {
    switch (this.result().status) {
      case 'provisioned': return 'Your workspace is ready';
      case 'pendingEmail': return 'Check your email';
      case 'pendingReview': return 'We have your registration';
      default: return 'Registration received';
    }
  });
}
