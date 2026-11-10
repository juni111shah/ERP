import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of } from 'rxjs';
import { BusinessProfile, NumberSequenceView } from '@core/api/setup.api';
import { AuthService } from '@core/auth/auth.service';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../../../icons';
import { meWith } from '@features/industry/industry.fixtures.spec';
import { SetupPage, sequenceExample } from './setup.page';

const PROFILE: BusinessProfile = {
  industryCode: 'log-cargo', industryLabel: 'Freight and cargo', industryGroup: 'Logistics', suggestedModules: ['logistics'], missingModules: [],
  capabilities: [
    { key: 'budgets', label: 'Budgets', description: 'Plan spending against a budget', enabled: true },
    { key: 'cheques', label: 'Cheques', description: 'Track cheques in custody', enabled: false },
  ],
  units: ['pallet', 'kg'], itemCategories: ['Spares'], dashboardFocus: [],
};
const SEQUENCES: NumberSequenceView[] = [{ documentType: 'invoice', prefix: 'INV', pattern: '{prefix}-{seq:4}', resetYearly: false, nextValue: 12, example: 'INV-0012' }];
const INDUSTRIES = [
  { key: 'log-cargo', label: 'Freight and cargo', description: '', availability: 'Available', suggestedModules: [], group: 'Logistics', sortOrder: 1 },
  { key: 'health-clinic', label: 'Clinic', description: '', availability: 'Available', suggestedModules: [], group: 'Healthcare', sortOrder: 2 },
  { key: 'soon', label: 'Not yet', description: '', availability: 'Preview', suggestedModules: [], group: 'Healthcare', sortOrder: 3 },
];

describe('sequenceExample', () => {
  it('fills the prefix, the year and the padded number', () => {
    const day = new Date(2026, 4, 1);
    expect(sequenceExample('{prefix}-{seq:4}', 'INV', 12, day)).toBe('INV-0012');
    expect(sequenceExample('{prefix}/{yy}/{seq:6}', 'TAX', 7, day)).toBe('TAX/26/000007');
    expect(sequenceExample('{yyyy}{seq:3}', 'X', 1234, day)).toBe('20261234');
  });
});

describe('Business setup page', () => {
  let fixture: ComponentFixture<SetupPage>;
  let http: HttpTestingController;
  const root = () => fixture.nativeElement as HTMLElement;
  const el = (id: string) => root().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const refreshed: string[] = [];

  beforeEach(async () => {
    refreshed.length = 0;
    await TestBed.configureTestingModule({
      imports: [SetupPage],
      providers: [
        provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting(), provideNzIcons(APP_ICONS),
        { provide: AuthService, useValue: { refresh: () => { refreshed.push('refresh'); return of('token'); }, loadMe: () => { refreshed.push('me'); return of({}); } } },
      ],
    }).compileComponents();
    TestBed.inject(SessionStore).me.set(meWith(['*']));
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SetupPage);
    fixture.detectChanges();
    http.expectOne('/api/v1/platform/industries').flush(INDUSTRIES);
    http.expectOne('/api/v1/setup/profile').flush(PROFILE);
    http.expectOne('/api/v1/setup/sequences').flush(SEQUENCES);
    fixture.detectChanges();
  });
  afterEach(() => { fixture.destroy(); document.querySelectorAll('.cdk-overlay-container').forEach((e) => (e.innerHTML = '')); });

  it('shows the current business type and groups the choices without the ones that are not available', () => {
    expect(el('setup-current-industry')?.textContent).toBe('Freight and cargo');
    const groups = fixture.componentInstance.groups();
    expect(groups.map((g) => g.group)).toEqual(['Logistics', 'Healthcare']);
    expect(groups.flatMap((g) => g.items.map((i) => i.key))).toEqual(['log-cargo', 'health-clinic']);
  });

  it('asks for confirmation that a change only adds, then shows what was added and refreshes the session', () => {
    expect((el('setup-industry-change') as HTMLButtonElement).disabled).toBeTrue();
    fixture.componentInstance.industry.set('health-clinic');
    fixture.detectChanges();
    (el('setup-industry-change') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(document.body.querySelector('[data-testid="setup-industry-confirm"]')?.textContent).toContain('only adds');

    fixture.componentInstance.applyIndustry();
    const post = http.expectOne('/api/v1/setup/industry');
    expect(post.request.body).toEqual({ industry: 'health-clinic' });
    post.flush({ industryCode: 'health-clinic', modulesEnabled: ['clinic'], rolesAdded: ['Doctor'], accountsAdded: ['4100'], modulesNotAllowed: [] });
    http.expectOne('/api/v1/setup/profile').flush({ ...PROFILE, industryCode: 'health-clinic', industryLabel: 'Clinic' });
    fixture.detectChanges();
    const result = el('setup-industry-result')?.textContent ?? '';
    expect(result).toContain('clinic');
    expect(result).toContain('Doctor');
    expect(result).toContain('4100');
    expect(refreshed).toEqual(['refresh', 'me']);
  });

  it('saves the optional areas that were switched off', () => {
    expect((el('setup-capabilities-save') as HTMLButtonElement).disabled).toBeTrue();
    fixture.componentInstance.toggle('budgets', false);
    fixture.componentInstance.toggle('cheques', true);
    fixture.detectChanges();
    (el('setup-capabilities-save') as HTMLButtonElement).click();
    const put = http.expectOne('/api/v1/setup/capabilities');
    expect(put.request.body).toEqual({ disabled: ['budgets'] });
    put.flush({ ...PROFILE, capabilities: [{ ...PROFILE.capabilities[0], enabled: false }, { ...PROFILE.capabilities[1], enabled: true }] });
  });

  it('edits the lists of units and categories', () => {
    fixture.componentInstance.units.set(['pallet', 'kg', 'box']);
    fixture.detectChanges();
    (el('setup-lists-save') as HTMLButtonElement).click();
    const put = http.expectOne('/api/v1/setup/lists');
    expect(put.request.body).toEqual({ units: ['pallet', 'kg', 'box'], itemCategories: ['Spares'] });
    put.flush({ ...PROFILE, units: ['pallet', 'kg', 'box'] });
  });

  it('shows a live example of each number series while it is edited, and saves it', () => {
    expect(el('setup-seq-example-invoice')?.textContent).toBe('INV-0012');
    const pattern = el('setup-seq-pattern-invoice') as HTMLInputElement;
    pattern.value = '{prefix}/{yy}/{seq:6}';
    pattern.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(el('setup-seq-example-invoice')?.textContent).toMatch(/^INV\/\d\d\/000012$/);

    (el('setup-seq-save-invoice') as HTMLButtonElement).click();
    const put = http.expectOne('/api/v1/setup/sequences/invoice');
    expect(put.request.body).toEqual({ prefix: 'INV', pattern: '{prefix}/{yy}/{seq:6}', resetYearly: false });
    put.flush({ ...SEQUENCES[0], pattern: '{prefix}/{yy}/{seq:6}' });
  });

  it('shows the server\'s reason when a pattern is refused', () => {
    fixture.componentInstance.edit('invoice', { pattern: '{prefix}-{nope}' });
    fixture.detectChanges();
    (el('setup-seq-save-invoice') as HTMLButtonElement).click();
    http.expectOne('/api/v1/setup/sequences/invoice').flush({ status: 400, errors: { pattern: ['The pattern must contain {seq:N}, such as {seq:6}.'] } }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();
    expect(el('setup-seq-invoice')?.textContent).toContain('The pattern must contain');
  });
});
