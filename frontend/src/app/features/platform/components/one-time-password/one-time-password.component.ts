import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';

/** What the one-time password dialog shows. */
export interface OneTimeSecret {
  title: string;
  /** Who or what the password is for. */
  facts: { label: string; value: string }[];
  /** null: the email already has an account, which keeps its own password. */
  password: string | null;
}

/** A temporary password is shown once. There is no second chance to read it, and the dialog says so. */
@Component({
  selector: 'erp-one-time-password',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzAlertModule, NzButtonModule, NzIconModule, NzModalModule],
  styleUrl: './one-time-password.component.scss',
  templateUrl: './one-time-password.component.html',
})
export class OneTimePasswordComponent {
  readonly secret = input<OneTimeSecret | null>(null);
  readonly actionLabel = input('Done');
  readonly closed = output<void>();
  readonly copied = signal(false);
  readonly copyFailed = signal(false);

  close(): void {
    this.copied.set(false);
    this.copyFailed.set(false);
    this.closed.emit();
  }

  copy(text: string): void {
    this.copyFailed.set(false);
    const done = () => this.copied.set(true);
    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(done, () => this.copyFallback(text, done));
    } else {
      this.copyFallback(text, done);
    }
  }

  /** Older browsers and pages not served over HTTPS have no clipboard API. If this fails too, the person copies it by hand. */
  private copyFallback(text: string, done: () => void): void {
    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();
    if (document.execCommand('copy')) done();
    else this.copyFailed.set(true);
    area.remove();
  }
}
