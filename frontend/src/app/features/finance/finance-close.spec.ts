import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../icons';
import { meWith } from '@features/industry/industry.fixtures.spec';
import { ApprovalRulesPage } from '@features/admin/pages/approval-rules/approval-rules.page';
import { ApprovalsPage } from '@features/admin/pages/approvals/approvals.page';
import { LedgerReportsPage } from './pages/ledger-reports/ledger-reports.page';
import { needsParty, OpeningBalancesPage } from './pages/opening-balances/opening-balances.page';
import { YearEndPage } from './pages/year-end/year-end.page';

const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1, hasNext: false, hasPrevious: false });

function setup(permissions: string[]) {
  // Row actions go behind a menu without a hover pointer, as in a headless browser; these specs are about which controls exist.
  spyOn(window, 'matchMedia').and.returnValue({ matches: true, addEventListener() {}, removeEventListener() {} } as unknown as MediaQueryList);
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([]), provideNzIcons(APP_ICONS)] });
  TestBed.inject(SessionStore).me.set(meWith(permissions, []));
  return TestBed.inject(HttpTestingController);
}

describe('year-end close', () => {
  it('closes a year with the entity and year, and reopens the latest closed year with a reason', () => {
    const http = setup(['finance.reports.read', 'finance.yearend.close', 'finance.yearend.reopen']);
    const f = TestBed.createComponent(YearEndPage);
    f.detectChanges();
    const closed = (year: number, extra = {}) => ({ id: 'y' + year, legalEntityId: 'le1', fiscalYear: year, yearStart: `${year}-01-01`, yearEnd: `${year}-12-31`, journalEntryId: 'j' + year, netResult: 100, previousBooksLockedTo: null, closedAt: '2027-01-02T00:00:00Z', closedBy: null, reopenedAt: null, reopenedBy: null, reopenReason: null, reversalJournalId: null, alreadyClosed: false, ...extra });
    http.expectOne((r) => r.url.endsWith('/finance/year-end')).flush(page([closed(2025), closed(2024)]));
    f.detectChanges();
    const root = f.nativeElement as HTMLElement;
    // Only the latest closed year offers Reopen.
    expect(f.componentInstance.canReopen(closed(2025))).toBeTrue();
    expect(f.componentInstance.canReopen(closed(2024))).toBeFalse();
    expect(root.querySelectorAll('[data-testid="year-reopen"]').length).toBeLessThanOrEqual(1);

    f.componentInstance.closeForm.show();
    f.componentInstance.closeForm.submit({ legalEntityId: 'le1', fiscalYear: 2026 });
    const close = http.expectOne((r) => r.url.endsWith('/finance/year-end/close'));
    expect(close.request.body).toEqual({ legalEntityId: 'le1', fiscalYear: 2026 });
    close.flush(closed(2026));
    http.expectOne((r) => r.url.endsWith('/finance/year-end') && r.method === 'GET').flush(page([]));

    f.componentInstance.reopenForm.show(closed(2025));
    f.componentInstance.reopenForm.submit({ reason: ' Posted to the wrong year ' });
    const reopen = http.expectOne((r) => r.url.endsWith('/finance/year-end/reopen'));
    expect(reopen.request.body).toEqual({ legalEntityId: 'le1', fiscalYear: 2025, reason: 'Posted to the wrong year' });
    reopen.flush(closed(2025, { reopenedAt: '2027-02-01T00:00:00Z' }));
    http.expectOne((r) => r.url.endsWith('/finance/year-end') && r.method === 'GET').flush(page([]));
  });

  it('hides Close a year from someone without the permission', () => {
    const http = setup(['finance.reports.read', 'finance.yearend.reopen']);
    const f = TestBed.createComponent(YearEndPage);
    f.detectChanges();
    http.expectOne((r) => r.url.endsWith('/finance/year-end')).flush(page([]));
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[data-testid="year-close"]')).toBeNull();
  });
});

describe('opening balances', () => {
  it('knows which accounts need a party', () => {
    expect(needsParty({ systemRole: 'ar.control' })).toBeTrue();
    expect(needsParty({ systemRole: 'ap.control' })).toBeTrue();
    expect(needsParty({ systemRole: 'cash' })).toBeFalse();
    expect(needsParty(undefined)).toBeFalse();
  });

  it('posts the lines with party and due date, and lists only opening entries', () => {
    const http = setup(['finance.journals.read', 'finance.journals.post', 'finance.journals.reverse']);
    const f = TestBed.createComponent(OpeningBalancesPage);
    f.detectChanges();
    const list = http.expectOne((r) => r.url.endsWith('/finance/journals'));
    expect(list.request.params.getAll('filter')).toContain('sourceType:eq:opening');
    list.flush(page([]));
    http.expectOne((r) => r.url.endsWith('/finance/accounts')).flush(page([
      { id: 'a1', code: '1000', name: 'Cash', type: 'asset', isBank: true, isSystem: false, isActive: true, isPostable: true, customFields: {} },
      { id: 'a2', code: '1100', name: 'Receivables', type: 'asset', systemRole: 'ar.control', isBank: false, isSystem: true, isActive: true, isPostable: true, customFields: {} },
    ]));
    const p = f.componentInstance;
    p.set(0, { accountId: 'a1', debit: 500 });
    p.set(1, { accountId: 'a2', debit: 300, partyId: null });
    expect(p.canSave()).toBeFalse(); // the receivables line has no customer yet
    p.set(1, { partyId: 'c1', dueDate: new Date(2026, 0, 31) });
    expect(p.canSave()).toBeTrue();
    expect(p.equityDifference()).toBe(800);
    p.entityId = 'le1';
    p.date = new Date(2026, 0, 1);
    p.save();
    const post = http.expectOne((r) => r.url.endsWith('/finance/opening-balances'));
    expect(post.request.body.legalEntityId).toBe('le1');
    expect(post.request.body.date).toBe('2026-01-01');
    expect(post.request.body.lines).toEqual([
      { accountId: 'a1', debit: 500, credit: 0, partyId: null, dueDate: null, reference: null },
      { accountId: 'a2', debit: 300, credit: 0, partyId: 'c1', dueDate: '2026-01-31', reference: null },
    ]);
    post.flush({ journalId: 'j1', number: 'JV-1', legalEntityId: 'le1', date: '2026-01-01', status: 'posted', amount: 800, openingEquityAmount: 800, invoiceIds: ['i1'], billIds: [], reversalJournalId: null });
    http.expectOne((r) => r.url.endsWith('/finance/journals')).flush(page([]));
  });
});

describe('cash flow report', () => {
  it('runs the cash-flow statement on the new tab and shows the three sections', () => {
    const http = setup(['finance.reports.read']);
    const f = TestBed.createComponent(LedgerReportsPage);
    f.detectChanges();
    http.match((r) => !r.url.includes('/finance/reports/')).forEach((r) => r.flush(page([])));
    http.match((r) => r.url.includes('/finance/reports/')).forEach((r) => r.flush({ rows: [], totalDebit: 0, totalCredit: 0, balanced: true }));
    f.componentInstance.selectTab(6);
    const call = http.expectOne((r) => r.url.endsWith('/finance/reports/cash-flow'));
    expect(call.request.params.get('from')).toMatch(/^\d{4}-01-01$/);
    call.flush({
      from: '2026-01-01', to: '2026-03-31', legalEntityId: null, currencyCode: 'AED', openingBalance: 1000,
      sections: [
        { category: 'operating', inflows: 500, outflows: 200, net: 300, lines: [{ group: 'Sales', code: '4000', name: 'Sales', inflows: 500, outflows: 200, net: 300 }] },
        { category: 'investing', inflows: 0, outflows: 0, net: 0, lines: [] },
        { category: 'financing', inflows: 0, outflows: 0, net: 0, lines: [] },
      ],
      netChange: 300, closingBalance: 1300, ledgerClosingBalance: 1300, tiesOut: true, internalTransfers: 0,
    });
    f.detectChanges();
    const text = (f.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Cash from operating activities');
    expect(text).toContain('Cash from investing activities');
    expect(text).toContain('Cash from financing activities');
    expect((f.nativeElement as HTMLElement).querySelector('[data-testid="cf-ties"]')).not.toBeNull();
  });
});

describe('approvals', () => {
  it('shows the level a request waits at and its steps', () => {
    const http = setup(['core.settings.read', 'finance.bills.approve']);
    const f = TestBed.createComponent(ApprovalsPage);
    f.detectChanges();
    const req = { id: 'r1', documentType: 'bill', subject: 'Bill 77', requestedBy: 'u2', requestedAt: '2026-03-01T00:00:00Z', status: 'pending', currentLevel: 2, currentApproverPermission: 'finance.bills.approve',
      steps: [{ id: 's1', level: 1, approverPermission: 'core.approvals.decide', status: 'approved', decidedAt: '2026-03-02T00:00:00Z' }, { id: 's2', level: 2, approverPermission: 'finance.bills.approve', status: 'pending' }] };
    http.expectOne((r) => r.url.endsWith('/approvals') && r.method === 'GET').flush([req]);
    f.detectChanges();
    const root = f.nativeElement as HTMLElement;
    expect(root.querySelector('[data-testid="approval-level"]')?.textContent).toContain('Level 2 of 2');
    // Holding the current level's permission is enough, without the core decide permission (the server checks the level's permission).
    expect(root.querySelector('[data-testid="approve"]')).not.toBeNull();
    f.componentInstance.showSteps(req as never);
    http.expectOne((r) => r.url.endsWith('/approvals/r1')).flush(req);
    f.detectChanges();
    expect(document.body.textContent).toContain('finance.bills.approve');
    document.querySelectorAll('.cdk-overlay-container').forEach((e) => (e.innerHTML = ''));
  });

  it('sends level and approver permission with a new rule', () => {
    const http = setup(['core.settings.read', 'core.settings.write']);
    const f = TestBed.createComponent(ApprovalRulesPage);
    f.detectChanges();
    http.expectOne((r) => r.url.endsWith('/approval-policies') && r.method === 'GET').flush([]);
    http.match((r) => r.url.endsWith('/permissions')).forEach((r) => r.flush([{ key: 'finance.bills.approve', module: 'finance', resource: 'bills', action: 'approve', description: '' }]));
    const p = f.componentInstance;
    p.documentType = 'journal';
    p.level = 2;
    p.approverPermission = 'finance.bills.approve';
    p.minAmount = 1000;
    p.add();
    const save = http.expectOne((r) => r.url.endsWith('/approval-policies') && r.method === 'POST');
    expect(save.request.body).toEqual({ documentType: 'journal', minAmount: 1000, isActive: true, level: 2, approverPermission: 'finance.bills.approve' });
    save.flush({});
    http.expectOne((r) => r.url.endsWith('/approval-policies') && r.method === 'GET').flush([]);
    expect(p.approverPermissions()).toContain('finance.bills.approve');
  });
});
