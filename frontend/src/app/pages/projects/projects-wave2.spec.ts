import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { ProjectsExtraApi } from '../../core/api/projects-extra.api';
import { taskToInput } from '../../core/api/projects.api';
import { SessionStore } from '../../core/session/session.store';
import { ProjectApprovalsPage } from './project-approvals.page';
import { ProjectBoardPage } from './project-board.page';
import { ProjectRisksPage } from './project-risks.page';
import { ProjectTimesheetsPage } from './project-timesheets.page';
import { statusLabel, statusOption, TASK_MOVES, taskOptions } from './projects-ui';

// Values as the API really sends them: camelCase (see Program.cs). A test that used "Todo" here would pass while the screen shows nothing.
const task = (id: string, status: string, extra: Record<string, unknown> = {}) => ({ id, projectId: 'p1', projectName: 'P', number: 'TSK-' + id, title: 'Task ' + id, status, priority: 'medium', estimatedHours: 8, actualHours: 2, createdAt: '2026-06-01', ...extra });
const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 200 });
const allowed = { hasPermission: () => true, me: () => ({ user: { id: 'u1' }, legalEntities: [] }), currentEntityId: () => null, orgId: () => null };

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([]), { provide: SessionStore, useValue: allowed }] });
  return TestBed.inject(HttpTestingController);
}

describe('project status names', () => {
  it('names and colours the values the API sends', () => {
    expect(statusLabel('task', 'inProgress')).toBe('In progress');
    expect(statusLabel('project', 'onHold')).toBe('On hold');
    expect(statusOption('milestone', 'achieved').label).toBe('Ready to bill');
    expect(statusOption('project', 'active').color).toBe('green');
  });
  it('offers only the moves the server allows', () => {
    expect(taskOptions('todo').map((o) => o.value).sort()).toEqual(['blocked', 'cancelled', 'inProgress', 'todo']);
    expect(TASK_MOVES.todo).not.toContain('done');
  });
  it('keeps every field when a task is saved for a small change', () => {
    const body = taskToInput({ ...task('1', 'todo', { labels: 'a,b', startDate: '2026-06-01', parentTaskId: 'x', assignedUserId: 'u2', milestoneId: 'm' }) } as never);
    expect(body.labels).toBe('a,b');
    expect(body.parentTaskId).toBe('x');
    expect(body.assignedUserId).toBe('u2');
    expect(body.milestoneId).toBe('m');
  });
});

describe('projects extra api', () => {
  it('previews a bill, saves a week and decides time with the right bodies', () => {
    const http = setup();
    const api = TestBed.inject(ProjectsExtraApi);
    api.previewBill({ projectId: 'p1', grouping: 'person', discountPercent: 10 }).subscribe();
    const preview = http.expectOne((r) => r.url.endsWith('/m/projects/billing/preview'));
    expect(preview.request.method).toBe('POST');
    expect(preview.request.body).toEqual({ projectId: 'p1', grouping: 'person', discountPercent: 10 });
    preview.flush({ projectId: 'p1', lines: [], subtotal: 0, discount: 0, total: 0, timesheets: 0, milestones: 0, expenses: 0 });

    api.saveWeek([{ projectId: 'p1', date: '2026-06-01', hours: 4 }]).subscribe();
    expect(http.expectOne((r) => r.url.endsWith('/m/projects/timesheets/week')).request.body).toEqual({ items: [{ projectId: 'p1', date: '2026-06-01', hours: 4 }] });

    api.decideTime(['t1'], false, 'Wrong project').subscribe();
    expect(http.expectOne((r) => r.url.endsWith('/m/projects/timesheets/decide')).request.body).toEqual({ ids: ['t1'], approve: false, reason: 'Wrong project' });

    api.utilisation('2026-06-01', '2026-06-30').subscribe();
    const util = http.expectOne((r) => r.url.endsWith('/m/projects/reports/utilisation'));
    expect(util.request.params.get('from')).toBe('2026-06-01');
    http.verify();
  });
});

describe('task board', () => {
  it('puts each task in the column of its status and moves a card with the server rule', () => {
    const http = setup();
    const fixture = TestBed.createComponent(ProjectBoardPage);
    fixture.detectChanges();
    http.match((r) => r.url.endsWith('/m/projects/projects')).forEach((r) => r.flush(page([])));
    http.match((r) => r.url.endsWith('/users') || r.url.endsWith('/m/projects/config')).forEach((r) => r.flush([]));
    http.expectOne((r) => r.url.endsWith('/m/projects/tasks')).flush(page([task('1', 'todo'), task('2', 'todo'), task('3', 'done')]));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid="col-todo"] article').length).toBe(2);
    expect(el.querySelectorAll('[data-testid="col-done"] article').length).toBe(1);

    fixture.componentInstance.move(fixture.componentInstance.tasks()[0], 'inProgress');
    fixture.detectChanges();
    expect(el.querySelectorAll('[data-testid="col-inProgress"] article').length).toBe(1);   // shown at once
    const put = http.expectOne((r) => r.url.endsWith('/m/projects/tasks/1/status'));
    expect(put.request.body).toEqual({ status: 'inProgress' });
    put.flush({ detail: 'Finish TSK-0 first' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(el.querySelectorAll('[data-testid="col-inProgress"] article').length).toBe(0);   // put back when the server says no
    expect(el.querySelectorAll('[data-testid="col-todo"] article').length).toBe(2);
  });
});

describe('risks page', () => {
  it('opens, which needs the modal service in its own imports', () => {
    const http = setup();
    const fixture = TestBed.createComponent(ProjectRisksPage);
    fixture.detectChanges();
    http.match((r) => r.url.includes('/m/projects/projects')).forEach((r) => r.flush(page([])));
    http.match((r) => r.url.endsWith('/users')).forEach((r) => r.flush([]));
    http.expectOne((r) => r.url.endsWith('/m/projects/risks')).flush([{ id: 'r1', projectId: 'p1', kind: 'risk', title: 'Late supplier', severity: 'high', status: 'open', createdAt: '2026-06-01' }, { id: 'r2', projectId: 'p1', kind: 'issue', title: 'Solved', severity: 'low', status: 'closed', createdAt: '2026-06-01' }]);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid="risk-table"] tbody tr').length).toBe(1);   // closed ones are behind a filter
    expect(el.textContent).toContain('Late supplier');
    fixture.componentInstance.statusFilter.set('all');
    fixture.detectChanges();
    expect(el.querySelectorAll('[data-testid="risk-table"] tbody tr').length).toBe(2);
  });
});

describe('approvals inbox', () => {
  it('groups what is waiting by person with the project on each line and approves the chosen lines', () => {
    const http = setup();
    const fixture = TestBed.createComponent(ProjectApprovalsPage);
    fixture.detectChanges();
    const time = [{ id: 't1', date: '2026-06-01', workerName: 'Ann', projectId: 'p1', projectName: 'Fit-out', description: 'Design', hours: 3, totalAmount: 300 }, { id: 't2', date: '2026-06-02', workerName: 'Bob', projectId: 'p1', projectName: 'Fit-out', description: 'Build', hours: 5, totalAmount: 500 }];
    http.expectOne((r) => r.url.endsWith('/m/projects/timesheets')).flush(page(time));
    http.expectOne((r) => r.url.endsWith('/m/projects/expenses')).flush(page([]));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid="time-approvals"] section').length).toBe(2);
    expect(el.textContent).toContain('Fit-out');
    (el.querySelector('[data-testid="time-approve"]') as HTMLButtonElement).click();   // nothing picked: the button is disabled
    http.expectNone((r) => r.url.endsWith('/timesheets/decide'));

    fixture.componentInstance.selectAll();
    fixture.detectChanges();
    (el.querySelector('[data-testid="time-approve"]') as HTMLButtonElement).click();
    const decide = http.expectOne((r) => r.url.endsWith('/m/projects/timesheets/decide'));
    expect(decide.request.body).toEqual({ ids: ['t1', 't2'], approve: true, reason: undefined });
    decide.flush({ decided: 2 });
    http.match((r) => r.url.includes('/m/projects/')).forEach((r) => r.flush(page([])));
  });
});

describe('timesheets', () => {
  it('offers Submit on drafts and locks approved time while approval is on', () => {
    // Row actions go behind a menu without a hover pointer, as in a headless browser. This is about which actions a row offers.
    spyOn(window, 'matchMedia').and.returnValue({ matches: true, addEventListener() {}, removeEventListener() {} } as unknown as MediaQueryList);
    const http = setup();
    const fixture = TestBed.createComponent(ProjectTimesheetsPage);
    fixture.detectChanges();
    const row = (id: string, approvalStatus: string) => ({ id, projectId: 'p1', projectName: 'Fit-out', date: '2026-06-01', workerName: 'Ann', workerUserId: 'u1', hours: 2, hourlyRate: 10, totalAmount: 20, isBillable: true, isBilled: false, writtenOff: false, approvalStatus, createdAt: '2026-06-01' });
    http.expectOne((r) => r.url.endsWith('/m/projects/config')).flush({ timesheetApproval: true, expenseApproval: false, allowSelfApproval: false, maxHoursPerDay: 16, weeklyCapacityHours: 40, workDays: [1, 2, 3, 4, 5], defaultHourlyRate: 0 });
    http.match((r) => r.url.endsWith('/m/projects/timesheets')).forEach((r) => r.flush(page([row('t1', 'draft'), row('t2', 'approved')])));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Fit-out');
    expect(el.querySelector('[data-testid="timesheet-submit-t1"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="timesheet-edit-t1"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="timesheet-submit-t2"]')).toBeNull();
    expect(el.querySelector('[data-testid="timesheet-edit-t2"]')).toBeNull();
  });
});
