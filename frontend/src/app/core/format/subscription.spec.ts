import { SubscriptionSummary } from '../session/session.store';
import { blockedPageApplies, landsOnConsole, limitText, sellingPoints, subscriptionBanner, suggestWorkspaceCode, usagePercent, yearlySaving } from './subscription';

const summary = (over: Partial<SubscriptionSummary>): SubscriptionSummary => ({
  hasSubscription: true, status: 'active', access: 'full', planName: 'Business', trialEndsOn: null, trialDaysLeft: null, currentPeriodEnd: null, lapsedOn: null, isComplimentary: false, suspendedReason: null, ...over,
});

describe('subscription helpers', () => {
  it('works out what paying yearly saves', () => {
    expect(yearlySaving(49, 490)).toEqual({ amount: 98, percent: 17 });
    expect(yearlySaving(100, 1000)).toEqual({ amount: 200, percent: 17 });
    expect(yearlySaving(100, 1200)).toBeNull();
    expect(yearlySaving(100, 1300)).toBeNull();
    expect(yearlySaving(0, 0)).toBeNull();
  });

  it('suggests a workspace code the server accepts', () => {
    expect(suggestWorkspaceCode('Acme & Sons Ltd.')).toBe('acme-sons-ltd');
    expect(suggestWorkspaceCode('  Café Noir  ')).toBe('cafe-noir');
    expect(suggestWorkspaceCode('---')).toBe('');
    expect(suggestWorkspaceCode('A')).toBe('');
    expect(suggestWorkspaceCode(null)).toBe('');
    expect(suggestWorkspaceCode('x'.repeat(80)).length).toBe(40);
    expect(suggestWorkspaceCode('a'.repeat(39) + ' b')).toBe('a'.repeat(39));
    expect(suggestWorkspaceCode('Global Trading Company')).toMatch(/^[a-z0-9][a-z0-9-]{1,48}$/);
  });

  it('writes limits in plain words', () => {
    expect(limitText(5, 'user', 'users')).toBe('Up to 5 users');
    expect(limitText(1, 'legal entity', 'legal entities')).toBe('1 legal entity');
    expect(limitText(null, 'user', 'users')).toBe('No limit on users');
  });

  it('drops selling points that only repeat a limit', () => {
    expect(sellingPoints(['Up to 5 users', '1 legal entity', '3 legal entities', 'Unlimited users', 'Email support', 'Invite guest users'])).toEqual(['Email support', 'Invite guest users']);
  });

  it('never fills a usage bar past the end', () => {
    expect(usagePercent(2, 4)).toBe(50);
    expect(usagePercent(9, 4)).toBe(100);
    expect(usagePercent(9, null)).toBe(0);
  });

  it('warns about a trial only in its last week', () => {
    expect(subscriptionBanner(summary({ status: 'trialing', trialDaysLeft: 8 }))).toBeNull();
    expect(subscriptionBanner(summary({ status: 'trialing', trialDaysLeft: 7 }))?.text).toContain('in 7 days');
    expect(subscriptionBanner(summary({ status: 'trialing', trialDaysLeft: 1 }))?.text).toContain('tomorrow');
    expect(subscriptionBanner(summary({ status: 'trialing', trialDaysLeft: 0 }))?.text).toContain('today');
  });

  it('says when a payment is late and when changes are paused', () => {
    expect(subscriptionBanner(summary({ status: 'pastDue' }))?.tone).toBe('warning');
    const paused = subscriptionBanner(summary({ status: 'expired', access: 'readOnly' }));
    expect(paused?.tone).toBe('error');
    expect(paused?.text).toContain('Changes are paused');
    expect(subscriptionBanner(summary({ status: 'pastDue', access: 'readOnly' }))?.text).toContain('overdue');
  });

  it('shows no banner when there is nothing to say', () => {
    expect(subscriptionBanner(null)).toBeNull();
    expect(subscriptionBanner(summary({}))).toBeNull();
    expect(subscriptionBanner(summary({ hasSubscription: false, status: null }))).toBeNull();
    expect(subscriptionBanner(summary({ status: 'suspended', access: 'blocked' }))).toBeNull();
    expect(subscriptionBanner(summary({ isComplimentary: true }))).toBeNull();
  });

  it('starts only the platform team in the platform workspace on the console', () => {
    expect(landsOnConsole({ platformAdmin: true, isInternalWorkspace: true }, false)).toBeTrue();
    expect(landsOnConsole({ platformAdmin: true, isInternalWorkspace: true }, true)).toBeFalse();
    expect(landsOnConsole({ platformAdmin: true, isInternalWorkspace: false }, false)).toBeFalse();
    expect(landsOnConsole({ platformAdmin: false, isInternalWorkspace: true }, false)).toBeFalse();
    expect(landsOnConsole({}, false)).toBeFalse();
    expect(landsOnConsole(null, false)).toBeFalse();
  });

  it('keeps the billing page open when a workspace is blocked', () => {
    expect(blockedPageApplies('blocked', '/finance/invoices')).toBeTrue();
    expect(blockedPageApplies('blocked', '/')).toBeTrue();
    expect(blockedPageApplies('blocked', '/admin/billing')).toBeFalse();
    expect(blockedPageApplies('blocked', '/admin/billing?x=1')).toBeFalse();
    expect(blockedPageApplies('blocked', '/admin/billing-other')).toBeTrue();
    expect(blockedPageApplies('readOnly', '/finance/invoices')).toBeFalse();
    expect(blockedPageApplies(undefined, '/')).toBeFalse();
  });
});
