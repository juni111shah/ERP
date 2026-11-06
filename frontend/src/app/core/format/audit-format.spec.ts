import { auditFieldLabel, auditIsCode, auditIsId, auditIsPrimary, auditRecordType, auditSummary, auditTone, auditValue, initials, relativeTime } from './audit-format';

describe('audit format', () => {
  const t = (_: string, fallback: string) => fallback;

  it('colours by what the event did', () => {
    expect(auditTone('Created')).toBe('success');
    expect(auditTone('Deleted')).toBe('error');
    expect(auditTone('Updated')).toBe('info');
    expect(auditTone(null)).toBe('info');
  });

  it('words field names and drops a reference suffix', () => {
    expect(auditFieldLabel('WorkspaceAssistantEnabled')).toBe('Workspace assistant enabled');
    expect(auditFieldLabel('PartyId')).toBe('Party');
  });

  it('keeps a missing value apart from zero and from false', () => {
    expect(auditValue(null)).toBe('—');
    expect(auditValue('')).toBe('—');
    expect(auditValue('0')).toBe('0');
    expect(auditValue('False')).toBe('No');
    expect(auditValue('True')).toBe('Yes');
  });

  it('leaves ids exact and lays out stored JSON', () => {
    const id = '388c744f-5dc3-4214-b09b-b65aa12a6a78';
    expect(auditValue(id)).toBe(id);
    expect(auditValue('{"a":1}')).toBe('{\n  "a": 1\n}');
    expect(auditValue('{not json}')).toBe('{not json}');
  });

  it('sums up a change in one line', () => {
    expect(auditSummary({ action: 'Created', changedFields: [] }, t)).toBe('New record');
    expect(auditSummary({ action: 'Deleted', changedFields: [] }, t)).toBe('Removed');
    expect(auditSummary({ action: 'Updated', changedFields: ['Name'] }, t)).toBe('Name');
    expect(auditSummary({ action: 'Updated', changedFields: ['A', 'B', 'C', 'D', 'E'] }, t)).toBe('A, B, C +2');
    expect(auditSummary({ action: 'Updated', changedFields: [] }, t)).toBe('');
  });

  it('puts first what the event is about', () => {
    expect(auditIsPrimary('Updated', { old: 'a', new: 'b', changed: true })).toBeTrue();
    expect(auditIsPrimary('Updated', { old: 'a', new: 'a', changed: false })).toBeFalse();
    expect(auditIsPrimary('Created', { old: null, new: 'b', changed: true })).toBeTrue();
    expect(auditIsPrimary('Created', { old: null, new: null, changed: false })).toBeFalse();
    // A soft delete writes the same values back, so nothing reads as changed; its values still matter.
    expect(auditIsPrimary('Deleted', { old: 'a', new: 'a', changed: false })).toBeTrue();
    expect(auditIsPrimary('Deleted', { old: null, new: null, changed: false })).toBeFalse();
  });

  it('recognises ids and stored JSON', () => {
    expect(auditIsId('388c744f-5dc3-4214-b09b-b65aa12a6a78')).toBeTrue();
    expect(auditIsId('Northwind')).toBeFalse();
    expect(auditIsId(null)).toBeFalse();
    expect(auditIsCode('{"a":1}')).toBeTrue();
    expect(auditIsCode('Northwind')).toBeFalse();
  });

  it('names a record type as people call it', () => {
    expect(auditRecordType('Settlement', 'Received')).toBe('Receipt');
    expect(auditRecordType('Settlement', '0')).toBe('Receipt');
    expect(auditRecordType('Settlement', 'Issued')).toBe('Payment');
    expect(auditRecordType('Settlement', '1')).toBe('Payment');
    expect(auditRecordType('Settlement')).toBe('Receipt or payment');
    expect(auditRecordType('PlatformSetting')).toBe('Platform setting');
  });

  it('makes initials from a name', () => {
    expect(initials('Platform Founder')).toBe('PF');
    expect(initials('Madonna')).toBe('MA');
    expect(initials('')).toBe('?');
  });

  it('says how long ago', () => {
    const now = new Date('2026-11-06T12:00:00Z');
    expect(relativeTime('2026-11-06T11:55:00Z', now)).toBe('5 minutes ago');
    expect(relativeTime('2026-11-05T12:00:00Z', now)).toBe('yesterday');
  });
});
