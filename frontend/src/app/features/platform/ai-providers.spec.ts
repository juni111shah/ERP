import { AssistantConsoleView } from '@core/api/platform.api';
import { AI_PROVIDER_PRESETS, coverageWarnings, moved, presetValues } from './ai-providers';

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

describe('coverageWarnings', () => {
  const provider = { id: '1', name: 'A', baseUrl: 'https://a.example', model: 'm', keyHint: 'abcd', keyNeedsReentry: false, lastTestedAt: null, lastTestOk: null, lastTestMessage: null };
  const view = (over: Partial<AssistantConsoleView>): AssistantConsoleView => ({ brandEnabled: false, workspaceEnabled: false, canManage: true, providers: [], brandDailyTokens: 1000, workspaceDailyTokens: 1000, usageToday: [], ...over });

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
});
