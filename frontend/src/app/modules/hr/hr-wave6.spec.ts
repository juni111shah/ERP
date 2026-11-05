import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { ShiftsApi } from './hr-people.api';

@Component({
  standalone: true,
  imports: [RecordDrawerComponent],
  template: `<erp-record-drawer title="Rule" [open]="true" [fields]="fields" [value]="value()" testId="rows-form" (submitted)="out = $event" />`,
})
class HostComponent {
  out: Record<string, unknown> | null = null;
  readonly value = signal<Record<string, unknown> | null>(null);
  readonly fields: FieldSpec[] = [
    { key: 'name', label: 'Name', type: 'text' },
    { key: 'slabs', label: 'Slabs', type: 'rows', columns: [{ key: 'fromAmount', label: 'From', type: 'number' }, { key: 'rate', label: 'Percent', type: 'number' }] },
  ];
}

function type(input: HTMLInputElement, text: string): void {
  input.value = text;
  input.dispatchEvent(new Event('input'));
}

describe('table editor field (rows)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()] }));

  it('adds lines, takes typed numbers and submits only the filled ones', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const root = document.body;
    const add = () => root.querySelector<HTMLButtonElement>('[data-testid="f-slabs-add"]')!;
    add().click(); add().click(); add().click();
    fixture.detectChanges();
    type(root.querySelector('[data-testid="f-slabs-0-fromAmount"]')!, '0');
    type(root.querySelector('[data-testid="f-slabs-0-rate"]')!, '0');
    type(root.querySelector('[data-testid="f-slabs-1-fromAmount"]')!, '1000');
    type(root.querySelector('[data-testid="f-slabs-1-rate"]')!, '10');
    // the third line stays empty and must be dropped
    root.querySelector<HTMLButtonElement>('[data-testid="rows-form-save"]')!.click();
    expect(fixture.componentInstance.out?.['slabs']).toEqual([{ fromAmount: 0, rate: 0 }, { fromAmount: 1000, rate: 10 }]);
  });

  it('shows the lines of the record being edited and lets one be removed', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.value.set({ name: 'Pension', slabs: [{ fromAmount: 0, rate: 0 }, { fromAmount: 2000, rate: 7 }] });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const root = document.body;
    expect(root.querySelector<HTMLInputElement>('[data-testid="f-slabs-1-rate"]')?.value).toBe('7');
    root.querySelector<HTMLButtonElement>('[aria-label="Remove line 1"]')!.click();
    fixture.detectChanges();
    root.querySelector<HTMLButtonElement>('[data-testid="rows-form-save"]')!.click();
    expect(fixture.componentInstance.out?.['slabs']).toEqual([{ fromAmount: 2000, rate: 7 }]);
  });
});

describe('shifts api', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));

  it('assigns employees through the shift and clears them through unassign', () => {
    const http = TestBed.inject(HttpTestingController);
    const api = TestBed.inject(ShiftsApi);
    api.assign('s1', ['e1', 'e2']).subscribe();
    const assign = http.expectOne((r) => r.url.endsWith('/m/hr/shifts/s1/assign'));
    expect(assign.request.method).toBe('POST');
    expect(assign.request.body).toEqual({ employeeIds: ['e1', 'e2'] });
    assign.flush({ assigned: 2 });
    api.unassign(['e1']).subscribe();
    const clear = http.expectOne((r) => r.url.endsWith('/m/hr/shifts/unassign'));
    expect(clear.request.body).toEqual({ employeeIds: ['e1'] });
    clear.flush({ cleared: 1 });
    http.verify();
  });
});
