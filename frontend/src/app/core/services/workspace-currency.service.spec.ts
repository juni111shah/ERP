import { TestBed } from '@angular/core/testing';
import { WorkspaceCurrencyService } from './workspace-currency.service';
import { SessionStore, MeResponse } from '../session/session.store';

describe('WorkspaceCurrencyService', () => {
  let service: WorkspaceCurrencyService;
  let sessionStore: SessionStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    sessionStore = TestBed.inject(SessionStore);
    service = TestBed.inject(WorkspaceCurrencyService);
  });

  it('defaults to QAR when no workspace or entity currency is present', () => {
    sessionStore.me.set(null);
    sessionStore.setEntity(null);
    TestBed.flushEffects();

    expect(service.currency()).toBe('QAR');
    expect(service.code()).toBe('QAR');
    expect(service.format(1234.5)).toBe('QAR 1,234.50');
  });

  it('returns workspace currency when no specific legal entity is selected', () => {
    const mockMe: MeResponse = {
      user: { id: 'u1', email: 'test@example.com', fullName: 'Tester', mustChangePassword: false },
      workspace: { id: 'w1', code: 'WS1', name: 'Test Org', countryCode: 'FR', baseCurrency: 'EUR', timeZone: 'UTC', defaultLocale: 'en' },
      workspaces: [],
      permissions: [],
      legalEntities: [
        { id: 'le-1', code: 'LE1', name: 'Entity 1', baseCurrency: 'GBP' }
      ],
      allLegalEntities: false,
      enabledModules: []
    };

    sessionStore.me.set(mockMe);
    sessionStore.setEntity(null);
    TestBed.flushEffects();

    expect(service.currency()).toBe('EUR');
    expect(service.code()).toBe('EUR');
    expect(service.format(50)).toBe('EUR 50.00');
  });

  it('returns legal entity currency when an active entity with baseCurrency is selected', () => {
    const mockMe: MeResponse = {
      user: { id: 'u1', email: 'test@example.com', fullName: 'Tester', mustChangePassword: false },
      workspace: { id: 'w1', code: 'WS1', name: 'Test Org', countryCode: 'FR', baseCurrency: 'EUR', timeZone: 'UTC', defaultLocale: 'en' },
      workspaces: [],
      permissions: [],
      legalEntities: [
        { id: 'le-1', code: 'LE1', name: 'Entity 1', baseCurrency: 'GBP' }
      ],
      allLegalEntities: false,
      enabledModules: []
    };

    sessionStore.me.set(mockMe);
    sessionStore.setEntity('le-1');
    TestBed.flushEffects();

    expect(service.currency()).toBe('GBP');
    expect(service.code()).toBe('GBP');
    expect(service.format(99.9)).toBe('GBP 99.90');
  });
});
