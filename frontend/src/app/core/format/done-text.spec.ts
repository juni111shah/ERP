import { doneText } from './done-text';

describe('doneText', () => {
  afterEach(() => document.documentElement.setAttribute('lang', 'en'));

  it('says which record and what happened', () => {
    expect(doneText('deleted', 'Promotion', 'Summer sale')).toBe('Promotion Summer sale deleted');
    expect(doneText('saved', 'Store', 'Downtown')).toBe('Store Downtown saved');
  });

  it('falls back to the entity when the record has no name', () => {
    expect(doneText('recorded', 'Cash movement')).toBe('Cash movement recorded');
    expect(doneText('saved', 'Store', '')).toBe('Store saved');
    expect(doneText('saved', 'Store', null)).toBe('Store saved');
  });

  it('keeps a zero as a real name', () => {
    expect(doneText('saved', 'Line', 0)).toBe('Line 0 saved');
  });

  it('reads as a whole sentence in Arabic, keeping the name', () => {
    document.documentElement.setAttribute('lang', 'ar');
    const text = doneText('deleted', 'Promotion', 'Summer sale');
    expect(text).toContain('تم حذف');
    expect(text).toContain('Summer sale');
  });
});
