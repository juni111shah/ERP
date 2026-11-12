import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { EngagementDto, ServicesRetainersApi } from '@core/api/services.api';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../icons';
import { OwnerPayoutsPage } from '@features/realestate/pages/owner-payouts/owner-payouts.page';
import { propertyFields } from '@features/realestate/realestate-forms';
import { RealEstateLookups } from '@core/api/realestate.api';
import { meWith } from '@features/industry/industry.fixtures.spec';
import { EngagementTimeComponent } from './components/engagement-time/engagement-time.component';

const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1, hasNext: false, hasPrevious: false });

function setup(permissions: string[]) {
  // Row actions go behind a menu without a hover pointer, as in a headless browser; these specs are about which controls exist.
  spyOn(window, 'matchMedia').and.returnValue({ matches: true, addEventListener() {}, removeEventListener() {} } as unknown as MediaQueryList);
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([]), provideNzIcons(APP_ICONS)] });
  TestBed.inject(SessionStore).me.set(meWith(permissions, ['services', 'realestate']));
  return TestBed.inject(HttpTestingController);
}

const ENGAGEMENT = { id: 'e1', legalEntityId: 'le1', engagementCode: 'ENG-1', title: 'Audit', clientId: 'c1', clientName: 'Acme', type: 'timeAndMaterials', status: 'active' } as EngagementDto;
const ENTRY = (id: string, approved: boolean, who = 'Ann') => ({ id, legalEntityId: 'le1', engagementId: 'e1', consultantName: who, activityDate: '2026-06-01', hours: 2, hourlyRate: 50, totalAmount: 100, taskDescription: 'Review', isBillable: true, isApproved: approved, invoiceId: null, createdAt: '2026-06-01' });

describe('engagement time entries', () => {
  function open(permissions = ['services.timeentries.read', 'services.timeentries.manage', 'services.engagements.manage']) {
    const http = setup(permissions);
    const f = TestBed.createComponent(EngagementTimeComponent);
    f.componentRef.setInput('engagement', ENGAGEMENT);
    f.detectChanges();
    http.expectOne((r) => r.url.endsWith('/m/services/time-entries')).flush(page([ENTRY('t1', false), ENTRY('t2', true)]));
    f.detectChanges();
    return { http, f };
  }

  it('approves without choosing an approver and filters the list to the engagement', () => {
    const { http, f } = open();
    f.componentInstance.approve(ENTRY('t1', false) as never);
    const call = http.expectOne((r) => r.url.endsWith('/m/services/time-entries/t1/approve'));
    expect(call.request.body).toEqual({});
    call.flush({});
    http.expectOne((r) => r.url.endsWith('/m/services/time-entries')).flush(page([]));
  });

  it('edits through PUT and deletes until approved; approved time has neither', () => {
    const { http, f } = open();
    const root = f.nativeElement as HTMLElement;
    expect(root.querySelectorAll('[data-testid="time-edit"]').length).toBe(1);
    expect(root.querySelectorAll('[data-testid="time-delete"]').length).toBe(1);
    f.componentInstance.form.show(ENTRY('t1', false) as never);
    f.componentInstance.form.submit({ activityDate: '2026-06-02', hours: 3, hourlyRate: 60, taskDescription: 'Review again', isBillable: true });
    const put = http.expectOne((r) => r.url.endsWith('/m/services/time-entries/t1') && r.method === 'PUT');
    expect(put.request.body.hours).toBe(3);
    expect(put.request.body.engagementId).toBe('e1');
    put.flush({});
    http.expectOne((r) => r.url.endsWith('/m/services/time-entries') && r.method === 'GET').flush(page([]));
    f.componentInstance.remove(ENTRY('t1', false) as never);
    http.expectOne((r) => r.url.endsWith('/m/services/time-entries/t1') && r.method === 'DELETE').flush(null);
    http.expectOne((r) => r.url.endsWith('/m/services/time-entries') && r.method === 'GET').flush(page([]));
  });

  it('lets the consultant\'s own entry wait for someone else to approve it', () => {
    const { f } = open();
    TestBed.inject(SessionStore).me.set({ ...meWith(['services.timeentries.manage'], ['services']), user: { id: 'u1', email: 'a@example.test', fullName: 'Ann', mustChangePassword: false } });
    f.detectChanges();
    const approve = (f.nativeElement as HTMLElement).querySelector('[data-testid="time-approve"]') as HTMLButtonElement;
    expect(approve.disabled).toBeTrue();
  });

  it('invoices the approved time of a time-and-materials engagement', () => {
    const { http, f } = open();
    expect(f.componentInstance.billable()).toBeTrue();
    f.componentInstance.invoiceTime();
    http.expectOne((r) => r.url.endsWith('/m/services/engagements/e1/invoice-time')).flush({ id: 'i1', number: 'INV-1', total: 100 });
    http.expectOne((r) => r.url.endsWith('/m/services/time-entries')).flush(page([]));
  });
});

describe('retainer invoice', () => {
  it('invoices the current period through the retainer route', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(ServicesRetainersApi).invoicePeriod('r1').subscribe();
    const call = http.expectOne((r) => r.url.endsWith('/m/services/retainers/r1/invoice'));
    expect(call.request.method).toBe('POST');
    expect(call.request.body).toEqual({});
  });
});

describe('owner payouts and the property form', () => {
  it('pays an owner with the entity, period and bank account, and a bill route without one', () => {
    const http = setup(['realestate.ownerpayouts.read', 'realestate.ownerpayouts.manage']);
    const f = TestBed.createComponent(OwnerPayoutsPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/finance/accounts')).forEach((r) => r.flush(page([])));
    http.expectOne((r) => r.url.endsWith('/m/realestate/owner-payouts')).flush(page([]));
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[data-testid="payout-new"]')).not.toBeNull();
    f.componentInstance.pay.show();
    f.componentInstance.pay.submit({ legalEntityId: 'le1', ownerPartyId: 'p1', from: '2026-01-01', to: '2026-03-31', date: '2026-04-05', method: 'payment', bankAccountId: 'b1', notes: '' });
    const call = http.expectOne((r) => r.url.endsWith('/m/realestate/owner-payouts') && r.method === 'POST');
    expect(call.request.body).toEqual(jasmine.objectContaining({ ownerPartyId: 'p1', method: 'payment', bankAccountId: 'b1', dueDate: null }));
    call.flush({ id: 'o1', number: 'OWP-1' });
    http.expectOne((r) => r.url.endsWith('/m/realestate/owner-payouts') && r.method === 'GET').flush(page([]));
    f.componentInstance.pay.show();
    f.componentInstance.pay.submit({ legalEntityId: 'le1', ownerPartyId: 'p1', from: '2026-04-01', to: '2026-06-30', date: '2026-07-05', method: 'bill', bankAccountId: 'b1', dueDate: '2026-07-30' });
    const bill = http.expectOne((r) => r.url.endsWith('/m/realestate/owner-payouts') && r.method === 'POST');
    expect(bill.request.body).toEqual(jasmine.objectContaining({ method: 'bill', bankAccountId: null, dueDate: '2026-07-30' }));
  });

  it('offers where rent is credited only on a property managed for an owner', () => {
    setup([]);
    const fields = propertyFields(TestBed.inject(RealEstateLookups));
    const credit = fields.find((x) => x.key === 'creditRentToOwner')!;
    expect(credit).toBeDefined();
    expect(credit.hidden!({ ownershipType: 'owned' })).toBeTrue();
    expect(credit.hidden!({ ownershipType: 'managedForOwner' })).toBeFalse();
    expect(credit.options!.map((o) => o.value)).toEqual([true, false]);
  });
});
