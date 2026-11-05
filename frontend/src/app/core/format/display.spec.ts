import { displayDate, displayLabel, isIsoDate, statusTone } from './display';

describe('display helpers', () => {
  it('writes API values as words and leaves text for people alone', () => {
    expect(displayLabel('closedWon')).toBe('Closed won');
    expect(displayLabel('active')).toBe('Active');
    expect(displayLabel('PartiallyReceived')).toBe('Partially received');
    expect(displayLabel('tax-return')).toBe('Tax return');
    expect(displayLabel('Finance & Accounting')).toBe('Finance & Accounting');
    expect(displayLabel('INV-2026-0001')).toBe('INV-2026-0001');
    expect(displayLabel(null)).toBe('');
  });

  it('writes dates as dd MMM yyyy, with three-letter months', () => {
    expect(displayDate('2026-11-05')).toBe('05 Nov 2026');
    expect(displayDate('2026-09-05')).toBe('05 Sep 2026');
  });

  it('never shifts a date that has no time', () => {
    expect(displayDate('2026-01-01')).toBe('01 Jan 2026');
    expect(displayDate('2026-12-31')).toBe('31 Dec 2026');
  });

  it('leaves anything that is not an ISO date as it came', () => {
    expect(displayDate('SO-2023-06-42')).toBe('SO-2023-06-42');
    expect(displayDate('Not set')).toBe('Not set');
    expect(displayDate('')).toBe('');
    expect(isIsoDate('2026-11-05')).toBeTrue();
    expect(isIsoDate('2026-11-05T10:00:00Z')).toBeTrue();
    expect(isIsoDate('SO-2023-06-42')).toBeFalse();
  });

  it('colours a status by its meaning, in any letter case', () => {
    expect(statusTone('closedWon')).toBe('success');
    expect(statusTone('ClosedWon')).toBe('success');
    expect(statusTone('closedLost')).toBe('error');
    expect(statusTone('draft')).toBe('warning');
    expect(statusTone('inProgress')).toBe('info');
    expect(statusTone('somethingNew')).toBe('neutral');
  });
});
