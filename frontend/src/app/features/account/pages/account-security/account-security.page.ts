import { TranslatePipe } from '@core/i18n/translate.pipe';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '@core/api/api-config';
import { asProblem } from '@core/http/problem-details';

/** Two-factor authentication with an authenticator app: set up, confirm with the first code, keep the recovery codes, switch off. */
@Component({
  selector: 'erp-account-security',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, FormsModule, NzAlertModule, NzButtonModule, NzCardModule, NzInputModule],
  templateUrl: './account-security.page.html',
})
export class AccountSecurityPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly message = inject(NzMessageService);
  readonly enabled = signal(false);
  readonly secret = signal('');
  readonly uri = signal('');
  readonly recovery = signal<string[]>([]);
  readonly busy = signal(false);
  code = '';
  password = '';

  constructor() { this.refresh(); }

  private refresh(): void { this.http.get<{ enabled: boolean }>(`${this.base}/auth/2fa`).subscribe((r) => this.enabled.set(r.enabled)); }

  private fail(e: unknown, fallback: string): void { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? fallback); }

  begin(): void {
    this.busy.set(true);
    this.http.post<{ secret: string; otpAuthUri: string }>(`${this.base}/auth/2fa/setup`, {}).subscribe({
      next: (r) => { this.busy.set(false); this.secret.set(r.secret); this.uri.set(r.otpAuthUri); },
      error: (e) => this.fail(e, 'Could not start the setup'),
    });
  }

  enable(): void {
    this.busy.set(true);
    this.http.post<{ recoveryCodes: string[] }>(`${this.base}/auth/2fa/enable`, { code: this.code }).subscribe({
      next: (r) => { this.busy.set(false); this.recovery.set(r.recoveryCodes); this.secret.set(''); this.code = ''; this.enabled.set(true); this.message.success('Two-factor authentication is now on for your account'); },
      error: (e) => this.fail(e, 'The code is incorrect'),
    });
  }

  disable(): void {
    this.busy.set(true);
    this.http.post(`${this.base}/auth/2fa/disable`, { password: this.password, code: this.code }).subscribe({
      next: () => { this.busy.set(false); this.password = ''; this.code = ''; this.recovery.set([]); this.enabled.set(false); this.message.success('Two-factor authentication is now off for your account'); },
      error: (e) => this.fail(e, 'Could not turn it off'),
    });
  }
}
