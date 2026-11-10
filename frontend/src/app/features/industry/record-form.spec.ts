import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of } from 'rxjs';
import { RecordTypeView } from '@core/api/industry.api';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../icons';
import { IndustryRecordFormComponent } from './components/industry-record-form/industry-record-form.component';
import { PACK, RECORD, SHIPMENTS, meWith } from './industry.fixtures.spec';
import { RecordFormContext, fieldSpecFor, linesTotal, recordFieldSpecs, toFormValue, toRecordInput } from './record-form';

const ctx: RecordFormContext = {
  legalEntities: [{ value: 'le1', label: 'MAIN · Main company' }], defaultLegalEntityId: null, businessUnits: [{ value: 'bu1', label: 'B1 · Branch' }],
  items: [{ value: 'i1', label: 'SKU1 · Crate' }], units: ['pallet', 'kg'], currency: 'AED', partyLookup: () => of([]), parentLookup: () => of([]),
};

describe('Record form mapping', () => {
  it('maps every field type of a definition to a form control', () => {
    const type = (t: string) => fieldSpecFor(SHIPMENTS.fields.find((f) => f.type === t)!).type;
    expect(type('text')).toBe('text');
    expect(type('textarea')).toBe('textarea');
    expect(type('number')).toBe('number');
    expect(type('money')).toBe('number');
    expect(type('date')).toBe('date');
    expect(type('bool')).toBe('switch');
    const mode = fieldSpecFor(SHIPMENTS.fields.find((f) => f.key === 'mode')!);
    expect(mode.type).toBe('select');
    expect(mode.options?.map((o) => o.value)).toEqual(['Road', 'Air']);
    expect(mode.required).toBeTrue();
    expect(fieldSpecFor(SHIPMENTS.fields.find((f) => f.type === 'money')!).min).toBe(0);
  });

  it('builds the whole form from the definition: pickers, dates, amount and a line grid', () => {
    const keys = recordFieldSpecs(SHIPMENTS, ctx).map((f) => f.key);
    expect(keys).toEqual(['title', 'partyId', 'parentId', 'd_origin', 'd_notes2', 'd_weight', 'd_cost', 'd_pickup', 'd_fragile', 'd_mode', 'legalEntityId', 'businessUnitId', 'recordDate', 'dueDate', 'amount', 'notes', 'lines']);
    const fields = recordFieldSpecs(SHIPMENTS, ctx);
    expect(fields[0].label).toBe('Consignment');
    expect(fields.find((f) => f.key === 'partyId')?.type).toBe('lookup');
    expect(fields.find((f) => f.key === 'lines')?.columns?.map((c) => c.key)).toEqual(['description', 'code', 'itemId', 'unitOfMeasure', 'quantity', 'unitPrice', 'dueDate']);
  });

  it('leaves out what the type does not have', () => {
    const plain: RecordTypeView = { ...SHIPMENTS, fields: [], partyRole: null, parentType: null, hasLines: false, hasAmount: false, hasDueDate: false, titleLabel: null };
    const noUnits = { ...ctx, businessUnits: [] };
    expect(recordFieldSpecs(plain, noUnits).map((f) => f.key)).toEqual(['title', 'legalEntityId', 'recordDate', 'notes']);
  });

  it('offers the workspace units in the grid, or free text when there are none, and a progress column when the type tracks progress', () => {
    const unit = (c: RecordFormContext, type = SHIPMENTS) => recordFieldSpecs(type, c).find((f) => f.key === 'lines')!.columns!.find((x) => x.key === 'unitOfMeasure')!;
    expect(unit(ctx).type).toBe('select');
    expect(unit({ ...ctx, units: [] }).type).toBe('text');
    expect(recordFieldSpecs({ ...SHIPMENTS, tracksProgress: true }, ctx).find((f) => f.key === 'lines')!.columns!.some((c) => c.key === 'completedQuantity')).toBeTrue();
  });

  it('adds up the lines', () => {
    expect(linesTotal([{ quantity: 2, unitPrice: 10.5 }, { quantity: 1, unitPrice: 0.25 }, {}])).toBe(21.25);
  });

  it('sends declared fields under data, drops empty ones, and keeps the lines', () => {
    const body = toRecordInput({
      title: 'Load', legalEntityId: 'le1', recordDate: '2026-03-01', partyId: 'p1', parentId: null, d_origin: 'Doha', d_weight: 12, d_cost: null, d_fragile: false, d_mode: 'Air', amount: 5,
      lines: [{ description: 'Freight', quantity: 2, unitPrice: 10, dueDate: '2026-03-05' }],
    }, SHIPMENTS);
    expect(body.data).toEqual({ origin: 'Doha', weight: 12, fragile: false, mode: 'Air' });
    expect(body.partyId).toBe('p1');
    expect(body.dueDate).toBeNull();
    expect(body.lines).toEqual([jasmine.objectContaining({ description: 'Freight', quantity: 2, unitPrice: 10, dueDate: '2026-03-05' })]);
  });

  it('turns a saved record back into form values', () => {
    const value = toFormValue(RECORD, SHIPMENTS);
    expect(value['d_origin']).toBe('Doha');
    expect(value['partyName']).toBe('Gulf Traders');
    expect(value['lines']).toEqual([jasmine.objectContaining({ description: 'Road freight', quantity: 1, unitPrice: 1500 })]);
  });
});

describe('Record form drawer', () => {
  let fixture: ComponentFixture<IndustryRecordFormComponent>;
  let http: HttpTestingController;
  const field = (id: string) => document.body.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IndustryRecordFormComponent],
      providers: [provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting(), provideNzIcons(APP_ICONS)],
    }).compileComponents();
    TestBed.inject(SessionStore).me.set(meWith([]));
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(IndustryRecordFormComponent);
    fixture.componentRef.setInput('module', PACK.key);
    fixture.componentRef.setInput('type', SHIPMENTS);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    http.match(() => true).forEach((r) => r.flush({ items: [], total: 0 }));
    fixture.detectChanges();
  });
  afterEach(() => { fixture.destroy(); document.querySelectorAll('.cdk-overlay-container').forEach((e) => (e.innerHTML = '')); });

  it('shows a control for each field the definition declares', () => {
    for (const id of ['f-title', 'f-partyId', 'f-parentId', 'f-d_origin', 'f-d_weight', 'f-d_pickup', 'f-d_fragile', 'f-d_mode', 'f-legalEntityId', 'f-recordDate', 'f-dueDate', 'f-amount', 'f-lines']) {
      expect(field(id)).withContext(id).not.toBeNull();
    }
  });

  it('lets lines be added and keeps a running total', () => {
    (field('f-lines-add') as HTMLButtonElement).click();
    fixture.detectChanges();
    const set = (id: string, value: string) => { const input = field(id) as HTMLInputElement; input.value = value; input.dispatchEvent(new Event('input')); fixture.detectChanges(); };
    set('f-lines-0-quantity', '3');
    set('f-lines-0-unitPrice', '12.5');
    expect(field('f-lines-summary')?.textContent).toContain('37.50');
  });
});
