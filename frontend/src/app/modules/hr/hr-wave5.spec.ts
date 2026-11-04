import { parseItems, itemsText } from './checklists.page';
import { parseImport, toTime } from './daily-attendance.page';
import { parseSlabs, slabsText } from './statutory-rules.page';
import { PHRASES_AR } from '../../core/i18n/phrases.ar';

describe('statutory rule slabs', () => {
  it('reads "from:percent" pairs and ignores junk', () => {
    expect(parseSlabs('0:0, 1000:10, 3000:20')).toEqual([{ fromAmount: 0, rate: 0 }, { fromAmount: 1000, rate: 10 }, { fromAmount: 3000, rate: 20 }]);
    expect(parseSlabs('')).toEqual([]);
    expect(parseSlabs('abc, 5:x, 7')).toEqual([]);
  });

  it('writes them back the same way', () => {
    expect(slabsText([{ fromAmount: 0, rate: 0 }, { fromAmount: 1000, rate: 10 }])).toBe('0:0, 1000:10');
    expect(slabsText(undefined)).toBe('');
  });
});

describe('checklist steps', () => {
  it('reads one step per line with its day offset', () => {
    expect(parseItems('Laptop | 0\nContract signed | 7\n\nExit interview | -3')).toEqual([
      { title: 'Laptop', dueOffsetDays: 0 }, { title: 'Contract signed', dueOffsetDays: 7 }, { title: 'Exit interview', dueOffsetDays: -3 },
    ]);
    expect(parseItems('No offset')).toEqual([{ title: 'No offset', dueOffsetDays: 0 }]);
    expect(parseItems(null)).toEqual([]);
  });

  it('round-trips through the text field', () => {
    const items = [{ title: 'Laptop', dueOffsetDays: 0 }, { title: 'Badge', dueOffsetDays: 2 }];
    expect(parseItems(itemsText(items))).toEqual(items);
  });
});

describe('daily attendance entry', () => {
  it('turns hh:mm into a time the server reads', () => {
    expect(toTime('8:05')).toBe('08:05:00');
    expect(toTime('17:30')).toBe('17:30:00');
    expect(toTime('')).toBeNull();
    expect(toTime('25:00')).toBeUndefined();
    expect(toTime('nine')).toBeUndefined();
  });

  it('reads an import by employee number and says what is wrong on which line', () => {
    const ids = new Map([['emp-001', 'id-1']]);
    const { rows, errors } = parseImport('EMP-001,2026-10-05,08:00,17:30\nEMP-001,2026-10-06,,\nEMP-999,2026-10-05,08:00,17:00\nEMP-001,05/10/2026,08:00,17:00\nEMP-001,2026-10-07,8am,17:00', ids);
    expect(rows).toEqual([
      { employeeId: 'id-1', date: '2026-10-05', checkIn: '08:00:00', checkOut: '17:30:00', source: 'import' },
      { employeeId: 'id-1', date: '2026-10-06', checkIn: null, checkOut: null, source: 'import' },
    ]);
    expect(errors).toEqual(['Line 3: unknown employee "EMP-999"', 'Line 4: date must be yyyy-mm-dd', 'Line 5: time must be hh:mm']);
  });
});

describe('Arabic phrases for the HR screens', () => {
  it('cover the new menu entries', () => {
    for (const label of ['My HR', 'Departments', 'Positions', 'Organisation', 'Document expiry', 'Loans and advances', 'HR reports', 'Daily attendance', 'Statutory rules', 'Recruitment', 'Checklists', 'HR tasks']) {
      expect(PHRASES_AR[label]).withContext(label).toBeTruthy();
    }
  });
});
