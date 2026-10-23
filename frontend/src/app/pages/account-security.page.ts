import { TranslatePipe } from '../core/i18n/translate.pipe';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '../core/api/api-config';
import { asProblem } from '../core/http/problem-details';

/** Two-factor authentication with an authenticator app: set up, confirm with the first code, keep the recovery codes, switch off. */
@Component({
  selector: 'erp-account-security',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, FormsModule, NzAlertModule, NzButtonModule, NzCardModule, NzInputModule],
  template: `
    <h1 class="erp-page-title">{{ 'security.title' | translate: 'Account security' }}</h1>
    <nz-card [nzTitle]="'security.twoFactor' | translate: 'Two-factor authentication'" style="max-width:640px">
      @if (enabled()) {
        <nz-alert nzType="success" nzShowIcon [nzMessage]="'security.on' | translate: 'Two-factor authentication is on.'" style="margin-bottom:16px" data-testid="tf-on"></nz-alert>
        <p>To switch it off, confirm your password and a current code.</p>
        <div style="display:flex;gap:8px;flex-wrap:wrap">
          <input nz-input type="password" placeholder="Password" [(ngModel)]="password" style="width:200px" data-testid="tf-password" />
          <input nz-input placeholder="Code" [(ngModel)]="code" style="width:140px" data-testid="tf-code" />
          <button nz-button nzDanger [nzLoading]="busy()" (click)="disable()" data-testid="tf-disable">{{ 'security.turnOff' | translate: 'Turn off' }}</button>
        </div>
      } @else if (!secret()) {
        <p>Protect your account with a code from an authenticator app (Google Authenticator, Microsoft Authenticator, Authy).</p>
        <button nz-button nzType="primary" [nzLoading]="busy()" (click)="begin()" data-testid="tf-begin">{{ 'security.setup' | translate: 'Set up' }}</button>
      } @else {
        <p>1. In your authenticator app choose <b>Enter a setup key</b> and type this key:</p>
        <p><code style="font-size:18px" data-testid="tf-secret">{{ secret() }}</code></p>
        <p style="word-break:break-all;opacity:.7">{{ 'ui.security.openLink' | translate: 'Or open this link on your phone' }}: {{ uri() }}</p>
        <p>2. Enter the 6-digit code the app shows:</p>
        <div style="display:flex;gap:8px">
          <input nz-input placeholder="123456" [(ngModel)]="code" style="width:140px" maxlength="7" data-testid="tf-code" />
          <button nz-button nzType="primary" [nzLoading]="busy()" (click)="enable()" data-testid="tf-enable">{{ 'security.turnOn' | translate: 'Turn on' }}</button>
        </div>
      }
      @if (recovery().length) {
        <nz-alert nzType="warning" nzShowIcon style="margin-top:16px" nzMessage="Save these recovery codes now. Each works once if you lose your phone. They will not be shown again."
                  [nzDescription]="codes" data-testid="tf-recovery"></nz-alert>
        <ng-template #codes><div style="display:grid;grid-template-columns:1fr 1fr;gap:4px;font-family:monospace">@for (c of recovery(); track c) { <span>{{ c }}</span> }</div></ng-template>
      }
    </nz-card>`,
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
      next: (r) => { this.busy.set(false); this.recovery.set(r.recoveryCodes); this.secret.set(''); this.code = ''; this.enabled.set(true); this.message.success('Two-factor authentication is on'); },
      error: (e) => this.fail(e, 'The code is incorrect'),
    });
  }

  disable(): void {
    this.busy.set(true);
    this.http.post(`${this.base}/auth/2fa/disable`, { password: this.password, code: this.code }).subscribe({
      next: () => { this.busy.set(false); this.password = ''; this.code = ''; this.recovery.set([]); this.enabled.set(false); this.message.success('Two-factor authentication is off'); },
      error: (e) => this.fail(e, 'Could not turn it off'),
    });
  }
}
