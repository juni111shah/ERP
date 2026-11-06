import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { catchError, forkJoin, of } from 'rxjs';
import { PlatformApi, SignupResult } from '../core/api/platform.api';
import { problemMessage } from '../core/http/problem-details';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { StatePanelComponent } from '../shared/state-panel.component';
import { RegisterResultComponent } from './register-result.component';

/** The link from the confirmation email. It works once, so it is sent once on load. */
@Component({
  selector: 'erp-register-verify',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzSpinModule, BrandLogoComponent, RegisterResultComponent, StatePanelComponent],
  styles: [`
    :host { display: grid; place-items: center; min-height: 100vh; padding: 24px; box-sizing: border-box; background: radial-gradient(circle at 10% 20%, rgba(16,149,93,.06) 0%, transparent 40%), var(--erp-color-bg-canvas); }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); width: 100%; max-width: 520px; border-radius: 16px; padding: 32px; box-sizing: border-box; }
    .brand { display: flex; justify-content: center; margin-bottom: 20px; }
    .brand erp-brand-logo { width: auto; }
    .busy { text-align: center; padding: 24px 0; color: var(--erp-color-text-muted); }
    .back { text-align: center; margin: 16px 0 0; }
    @media (max-width: 767px) { :host { padding: 16px; } .card { padding: 20px 16px; } }
  `],
  template: `
    <div class="card">
      <div class="brand"><erp-brand-logo size="lg"></erp-brand-logo></div>
      @if (busy()) {
        <div class="busy" data-testid="verify-busy"><nz-spin nzSimple></nz-spin><p style="margin:12px 0 0">Confirming your email...</p></div>
      } @else if (result(); as r) {
        <erp-register-result [result]="r" [supportEmail]="supportEmail()" />
      } @else {
        <erp-state-panel kind="error" title="This link did not work" [hint]="error()" data-testid="verify-error" />
        <p class="back"><a routerLink="/register" data-testid="verify-back">Back to registration</a></p>
      }
    </div>`,
})
export class RegisterVerifyPage {
  private readonly api = inject(PlatformApi);
  readonly busy = signal(true);
  readonly result = signal<SignupResult | null>(null);
  readonly error = signal<string | null>(null);
  readonly supportEmail = signal<string | null>(null);

  constructor() {
    const token = inject(ActivatedRoute).snapshot.queryParamMap.get('token');
    if (!token) {
      this.busy.set(false);
      this.error.set('This link is not complete. Please open the link from your email again.');
      return;
    }
    forkJoin({ result: this.api.verifySignup(token), config: this.api.signupConfig().pipe(catchError(() => of(null))) }).subscribe({
      next: ({ result, config }) => { this.result.set(result); this.supportEmail.set(config?.supportEmail ?? null); this.busy.set(false); },
      error: (e) => { this.error.set(problemMessage(e, 'This link has already been used or is no longer valid.')); this.busy.set(false); },
    });
  }
}
