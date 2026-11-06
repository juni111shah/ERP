import { auditFieldLabel, auditSummary, auditTone, auditValue, initials, relativeTime } from './audit-format';

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
