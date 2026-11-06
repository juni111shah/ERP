import { AssistantConsoleView, AssistantUsageRow } from '../../core/api/platform.api';
import { AI_PROVIDER_PRESETS, coverageWarnings, failureWarnings, moved, presetValues, usageTotals } from './ai-providers';

describe('AI service presets', () => {
  it('only offers secure addresses', () => {
    for (const p of AI_PROVIDER_PRESETS.filter((x) => x.baseUrl)) expect(p.baseUrl.startsWith('https://')).toBeTrue();
  });

  it('fills in the chosen service and ignores anything else', () => {
    expect(presetValues('groq')?.name).toBe('Groq');
    expect(presetValues('nothing')).toBeNull();
  });

  it('leaves everything empty for another service', () => {
    expect(presetValues('custom')).toEqual({ name: '', baseUrl: '', model: '' });
  });
});

describe('moved', () => {
  it('moves an item up or down', () => {
    expect(moved(['a', 'b', 'c'], 2, -1)).toEqual(['a', 'c', 'b']);
    expect(moved(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
  });

  it('stays put at either end', () => {
    expect(moved(['a', 'b'], 0, -1)).toEqual(['a', 'b']);
    expect(moved(['a', 'b'], 1, 1)).toEqual(['a', 'b']);
  });
});

describe('assistant console warnings', () => {
  const provider = { id: '1', name: 'A', baseUrl: 'https://a.example', model: 'm', keyHint: 'abcd', keyNeedsReentry: false, lastTestedAt: null, lastTestOk: null, lastTestMessage: null };
  const view = (over: Partial<AssistantConsoleView>): AssistantConsoleView => ({ brandEnabled: false, workspaceEnabled: false, canManage: true, providers: [], brandDailyTokens: 1000, workspaceDailyTokens: 1000, workspaceDailyMessagesPerClient: 200, usageToday: [], usageLast30Days: [], ...over });

  it('says nothing while the assistants are hidden', () => {
    expect(coverageWarnings(view({}))).toEqual([]);
  });

  it('warns when an assistant is on with nothing to answer for it', () => {
    expect(coverageWarnings(view({ brandEnabled: true })).length).toBe(1);
    expect(coverageWarnings(view({ workspaceEnabled: true, providers: [{ ...provider, useForBrand: true, useForWorkspace: false }] })).length).toBe(1);
  });

  it('is satisfied once a service covers each assistant', () => {
    expect(coverageWarnings(view({ brandEnabled: true, workspaceEnabled: true, providers: [{ ...provider, useForBrand: true, useForWorkspace: true }] }))).toEqual([]);
  });

  it('names a service in use that is failing, with the reason', () => {
    const warnings = failureWarnings(view({ providers: [{ ...provider, useForBrand: true, useForWorkspace: false, lastTestOk: false, lastTestMessage: 'The service rejected the key.' }] }));
    expect(warnings.length).toBe(1);
    expect(warnings[0]).toContain('A is failing: The service rejected the key.');
  });

  it('stays quiet about a working, untested or unused service', () => {
    expect(failureWarnings(view({ providers: [
      { ...provider, useForBrand: true, useForWorkspace: false, lastTestOk: true },
      { ...provider, useForBrand: true, useForWorkspace: false, lastTestOk: null },
      { ...provider, useForBrand: false, useForWorkspace: false, lastTestOk: false, lastTestMessage: 'A failed test.' },
    ] }))).toEqual([]);
  });
});

describe('usageTotals', () => {
  const row = (requests: number, failures: number, inputTokens: number, outputTokens: number): AssistantUsageRow => ({ bot: 'brand', providerId: '1', providerName: 'A', requests, failures, inputTokens, outputTokens, lastFailure: null });

  it('adds up requests, failures and both kinds of token', () => {
    expect(usageTotals([row(2, 1, 10, 5), row(3, 0, 20, 1)])).toEqual({ requests: 5, failures: 1, tokens: 36 });
  });

  it('is zero with nothing used', () => {
    expect(usageTotals([])).toEqual({ requests: 0, failures: 0, tokens: 0 });
  });
});
