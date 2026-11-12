import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { catchError, forkJoin, of } from 'rxjs';
import { PlatformApi, SignupResult } from '@core/api/platform.api';
import { problemMessage } from '@core/http/problem-details';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { RegisterResultComponent } from '../../components/register-result/register-result.component';

/** The link from the confirmation email. It works once, so it is sent once on load. */
@Component({
  selector: 'erp-register-verify',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzSpinModule, BrandLogoComponent, RegisterResultComponent, StatePanelComponent],
  styleUrl: './register-verify.page.scss',
  templateUrl: './register-verify.page.html',
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
