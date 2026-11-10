import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SignupResult } from '@core/api/platform.api';

/** What happens next after a registration: the same panel after the form and after the email link. */
@Component({
  selector: 'erp-register-result',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule],
  styleUrl: './register-result.component.scss',
  templateUrl: './register-result.component.html',
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
