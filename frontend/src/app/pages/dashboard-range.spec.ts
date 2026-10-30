import { convertToParamMap } from '@angular/router';
import { RANGE_KEY, RANGE_PRESETS, isRealDate, minusMonths, parseIso, periodStart, presetRange, rangeFromParams, storedRange, validRange } from './dashboard.page';

/** The overview period helpers: preset day math (matches the API's DashboardRange.StartFor), validation, URL and storage restore. */
describe('Overview date range', () => {
  const today = new Date(2026, 9, 30); // 30 Oct 2026

  it('offers the seven dropdown choices in order, with "Last year" as the trailing 12 months', () => {
    expect(RANGE_PRESETS.map((p) => p.label)).toEqual(['Last month', 'Last 3 months', 'Last 6 months', 'Last year', 'Last 2 years', 'Last 3 years']);
    expect(RANGE_PRESETS.find((p) => p.key === '1y')!.months).toBe(12);
  });

  it('starts each preset one day after the same day n months back', () => {
    expect(presetRange('1m', today)).toEqual({ preset: '1m', from: '2026-10-01', to: '2026-10-30' });
    expect(presetRange('3m', today).from).toBe('2026-07-31');
    expect(presetRange('6m', today).from).toBe('2026-05-01');
    expect(presetRange('1y', today).from).toBe('2025-10-31');
    expect(presetRange('2y', today).from).toBe('2024-10-31');
    expect(presetRange('3y', today).from).toBe('2023-10-31');
    // 1Y covers exactly 365 days (12 whole months), not 367
    expect(Math.round((today.getTime() - parseIso(presetRange('1y', today).from).getTime()) / 86_400_000) + 1).toBe(365);
  });

  it('clamps month ends like the server (31 Mar minus a month is 28 Feb, so the period starts 1 Mar)', () => {
    expect(minusMonths(new Date(2026, 2, 31), 1)).toEqual(new Date(2026, 1, 28));
    expect(periodStart(new Date(2026, 2, 31), 1)).toEqual(new Date(2026, 2, 1));
    expect(presetRange('1y', new Date(2026, 8, 30)).from).toBe('2025-10-01'); // ends on a month end: 12 calendar months
  });

  it('accepts real, ordered, past periods of at most 5 years', () => {
    expect(isRealDate('2026-02-28')).toBeTrue();
    expect(isRealDate('2026-02-31')).toBeFalse();
    expect(isRealDate('2026-2-1')).toBeFalse();
    expect(validRange('2026-01-01', '2026-10-30', today)).toBeTrue();
    expect(validRange('2021-10-31', '2026-10-30', today)).toBeTrue();   // exactly 5 years
    expect(validRange('2021-10-30', '2026-10-30', today)).toBeFalse();  // 5 years and a day
    expect(validRange('2026-10-30', '2026-10-01', today)).toBeFalse();  // reversed
    expect(validRange('2026-10-01', '2026-10-31', today)).toBeFalse();  // ends in the future
    expect(validRange('2026-02-01', '2026-02-31', today)).toBeFalse();  // not a date
  });

  it('reads presets and valid custom periods from the URL, and ignores the rest', () => {
    expect(rangeFromParams(convertToParamMap({ range: '3m' }), today)).toEqual(presetRange('3m', today));
    expect(rangeFromParams(convertToParamMap({ from: '2025-01-01', to: '2025-12-31' }), today)).toEqual({ preset: 'custom', from: '2025-01-01', to: '2025-12-31' });
    expect(rangeFromParams(convertToParamMap({ from: '2015-01-01', to: '2025-12-31' }), today)).toBeNull();
    expect(rangeFromParams(convertToParamMap({ from: '2026-01-01', to: '2027-01-01' }), today)).toBeNull();
    expect(rangeFromParams(convertToParamMap({ range: 'week' }), today)).toBeNull();
    expect(rangeFromParams(convertToParamMap({}), today)).toBeNull();
  });

  describe('browser storage', () => {
    afterEach(() => localStorage.removeItem(RANGE_KEY));

    it('restores a custom period as custom (so the picker shows) and re-anchors presets on today', () => {
      localStorage.setItem(RANGE_KEY, JSON.stringify({ preset: 'custom', from: '2025-03-01', to: '2025-06-30' }));
      expect(storedRange(today)).toEqual({ preset: 'custom', from: '2025-03-01', to: '2025-06-30' });
      localStorage.setItem(RANGE_KEY, JSON.stringify({ preset: '6m', from: '2020-01-01', to: '2020-06-30' }));
      expect(storedRange(today)).toEqual(presetRange('6m', today));
    });

    it('drops stored periods that are too long, in the future or corrupt', () => {
      localStorage.setItem(RANGE_KEY, JSON.stringify({ preset: 'custom', from: '2010-01-01', to: '2026-01-01' }));
      expect(storedRange(today)).toBeNull();
      localStorage.setItem(RANGE_KEY, JSON.stringify({ preset: 'custom', from: '2026-10-01', to: '2026-12-01' }));
      expect(storedRange(today)).toBeNull();
      localStorage.setItem(RANGE_KEY, '{not json');
      expect(storedRange(today)).toBeNull();
    });
  });
});
