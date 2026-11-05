import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { ProjectsExtraApi } from '../../core/api/projects-extra.api';
import { ProjectApprovalsPage } from './project-approvals.page';
import { ProjectBoardPage } from './project-board.page';

const task = (id: string, status: string, extra: Record<string, unknown> = {}) => ({ id, projectId: 'p1', projectName: 'P', number: 'TSK-' + id, title: 'Task ' + id, status, priority: 'Medium', estimatedHours: 8, actualHours: 2, createdAt: '2026-06-01', ...extra });
const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 500 });

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([])] });
  return TestBed.inject(HttpTestingController);
}

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
    http.expectOne((r) => r.url.endsWith('/m/projects/tasks')).flush(page([task('1', 'Todo'), task('2', 'Todo'), task('3', 'Done')]));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid="col-Todo"] article').length).toBe(2);
    expect(el.querySelectorAll('[data-testid="col-Done"] article').length).toBe(1);

    fixture.componentInstance.move(fixture.componentInstance.tasks()[0], 'InProgress');
    fixture.detectChanges();
    expect(el.querySelectorAll('[data-testid="col-InProgress"] article').length).toBe(1);   // shown at once
    const put = http.expectOne((r) => r.url.endsWith('/m/projects/tasks/1/status'));
    expect(put.request.body).toEqual({ status: 'InProgress' });
    put.flush({ detail: 'Finish TSK-0 first' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(el.querySelectorAll('[data-testid="col-InProgress"] article').length).toBe(0);   // put back when the server says no
    expect(el.querySelectorAll('[data-testid="col-Todo"] article').length).toBe(2);
  });
});

describe('timesheet approvals', () => {
  it('lists what is waiting and approves the chosen lines', () => {
    const http = setup();
    const fixture = TestBed.createComponent(ProjectApprovalsPage);
    fixture.detectChanges();
    const time = [{ id: 't1', date: '2026-06-01', workerName: 'Ann', description: 'Design', hours: 3, totalAmount: 300 }, { id: 't2', date: '2026-06-02', workerName: 'Bob', description: 'Build', hours: 5, totalAmount: 500 }];
    http.expectOne((r) => r.url.endsWith('/m/projects/timesheets')).flush(page(time));
    http.expectOne((r) => r.url.endsWith('/m/projects/expenses')).flush(page([]));
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelectorAll('[data-testid="time-approvals"] tbody tr').length).toBe(2);
    (el.querySelector('[data-testid="time-approve"]') as HTMLButtonElement).click();   // nothing picked: the button is disabled
    http.expectNone((r) => r.url.endsWith('/timesheets/decide'));

    fixture.componentInstance.toggleAllTime(true);
    fixture.detectChanges();
    (el.querySelector('[data-testid="time-approve"]') as HTMLButtonElement).click();
    const decide = http.expectOne((r) => r.url.endsWith('/m/projects/timesheets/decide'));
    expect(decide.request.body).toEqual({ ids: ['t1', 't2'], approve: true, reason: undefined });
    decide.flush({ decided: 2 });
    http.match((r) => r.url.includes('/m/projects/')).forEach((r) => r.flush(page([])));
  });
});
