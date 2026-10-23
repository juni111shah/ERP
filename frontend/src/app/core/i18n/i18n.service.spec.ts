import { TestBed } from '@angular/core/testing';
import { I18nService } from './i18n.service';

describe('I18nService', () => {
  let service: I18nService;
  beforeEach(() => {
    try { localStorage.removeItem('erp.ui.prefs'); } catch { /* ignore */ }
    TestBed.configureTestingModule({});
    service = TestBed.inject(I18nService);
  });

  it('defaults to English and returns the fallback or the key when a translation is missing', () => {
    expect(service.lang()).toBe('en');
    expect(service.t('common.save', 'Save')).toBe('Save');
    expect(service.t('no.such.key')).toBe('no.such.key');
  });

  it('switches to Arabic: translations, rtl direction and the html attributes', () => {
    service.setLang('ar');
    TestBed.flushEffects();
    expect(service.t('common.save', 'Save')).toBe('حفظ');
    expect(service.dir()).toBe('rtl');
    expect(document.documentElement.getAttribute('dir')).toBe('rtl');
    expect(document.documentElement.getAttribute('lang')).toBe('ar');
    service.toggle();
    TestBed.flushEffects();
    expect(service.dir()).toBe('ltr');
    expect(document.documentElement.getAttribute('dir')).toBe('ltr');
  });
});
