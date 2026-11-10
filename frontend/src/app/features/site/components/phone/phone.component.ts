import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { PhoneScreen, lastMonths } from '../../pricing-mockups';

/** One phone showing a screen of the mobile app: its real header, chips, cards and floating menu, with sample figures. A drawing rather than a screenshot, so it stays sharp at any size. */
@Component({
  selector: 'erp-phone',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyboardClickDirective, NzIconModule],
  styleUrl: './phone.component.scss',
  templateUrl: './phone.component.html',
})
export class PhoneComponent {
  readonly screen = input<PhoneScreen>('overview');
  readonly title = computed(() => ({ overview: 'Overview', invoices: 'Invoices', hr: 'My HR', approvals: 'Approvals', alerts: 'Notifications' })[this.screen()]);
  readonly months = lastMonths(12);
  readonly bars = [[44, 40], [50, 46], [47, 49], [56, 50], [60, 58], [55, 57], [64, 60], [70, 62], [66, 69], [76, 70], [84, 74], [90, 86]];
  readonly invoices = [
    { who: 'Northwind Trading', no: 'INV-2026-07-017', due: '25 Jul 2026', amt: '1,050.00', state: 'Paid', tone: 'paid' },
    { who: 'Blue Harbor Foods', no: 'INV-2026-06-018', due: '25 Jun 2026', amt: '2,480.00', state: 'Issued', tone: 'issued' },
    { who: 'Atlas Supplies', no: 'INV-2026-05-019', due: '25 May 2026', amt: '640.00', state: 'Overdue', tone: 'overdue' },
    { who: 'Cedar & Co', no: 'INV-2026-04-020', due: '25 Apr 2026', amt: '3,115.50', state: 'Paid', tone: 'paid' },
    { who: 'Summit Works', no: 'INV-2026-03-021', due: '25 Mar 2026', amt: '915.00', state: 'Paid', tone: 'paid' },
  ];
  readonly alerts = [
    { icon: 'check-circle', title: 'Receipt recorded for INV-2026-07-017', when: 'A minute ago', warn: false },
    { icon: 'check-square', title: 'A purchase order needs your approval', when: '20 minutes ago', warn: false },
    { icon: 'warning', title: '3 invoices are overdue', when: 'This morning', warn: true },
    { icon: 'calendar', title: 'Your leave request was approved', when: 'Yesterday', warn: false },
  ];
}
