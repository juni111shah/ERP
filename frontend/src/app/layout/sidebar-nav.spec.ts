import { KNOWN_ICONS } from '../icons';
import { FALLBACK_GROUP_ICON, GROUP_ICONS, NavGroup, currentGroupId, groupIcon, nextFocusIndex, resolveOpenGroup, scrollDeltaToReveal } from './sidebar-nav';

const item = (id: string, route: string) => ({ id, label: id, route, module: 'core' });
const groups: NavGroup[] = [
  { id: 'workspace', label: 'Workspace', order: 10, items: [item('overview', '/'), item('approvals', '/approvals')] },
  { id: 'sales', label: 'Sales', order: 30, items: [item('customers', '/parties?type=customer'), item('invoices', '/finance/invoices')] },
  { id: 'purchases', label: 'Purchases', order: 40, items: [item('vendors', '/parties?type=vendor')] },
  { id: 'masterdata', label: 'Master Data', order: 60, items: [item('parties', '/parties')] },
  { id: 'projects', label: 'Projects', order: 350, items: [item('list', '/projects')] },
  { id: 'projects.money', label: 'Money', order: 351, items: [item('time', '/projects/time')] },
];

describe('sidebar group icons', () => {
  it('gives each known group its own registered icon', () => {
    const icons = [...GROUP_ICONS.values()];
    expect(new Set(icons).size).toBe(icons.length);
    for (const icon of icons) expect(KNOWN_ICONS.has(icon)).withContext(icon).toBeTrue();
  });

  it('never uses the fallback icon for a known group, so unknown groups stand apart', () => {
    expect([...GROUP_ICONS.values()]).not.toContain(FALLBACK_GROUP_ICON);
    expect(KNOWN_ICONS.has(FALLBACK_GROUP_ICON)).toBeTrue();
  });

  it('maps known groups and falls back for unknown ones, including object property names', () => {
    expect(groupIcon('pos')).toBe('shop');
    expect(groupIcon('not-a-group')).toBe(FALLBACK_GROUP_ICON);
    expect(groupIcon('constructor')).toBe(FALLBACK_GROUP_ICON);
  });
});

describe('currentGroupId', () => {
  it('matches the root route only at the root', () => {
    expect(currentGroupId(groups, '/')).toBe('workspace');
    expect(currentGroupId(groups, '/somewhere-else')).toBeNull();
  });

  it('matches child pages but not routes that only share a prefix', () => {
    expect(currentGroupId(groups, '/finance/invoices/42')).toBe('sales');
    expect(currentGroupId(groups, '/finance/invoices-old')).toBeNull();
  });

  it('uses the query to separate groups linking to the same page', () => {
    expect(currentGroupId(groups, '/parties?type=customer')).toBe('sales');
    expect(currentGroupId(groups, '/parties?type=vendor&page=2')).toBe('purchases');
    expect(currentGroupId(groups, '/parties')).toBe('masterdata');
    expect(currentGroupId(groups, '/parties/7')).toBe('masterdata');
  });

  it('prefers the longest route path', () => {
    expect(currentGroupId(groups, '/projects/time/3')).toBe('projects.money');
    expect(currentGroupId(groups, '/projects/3')).toBe('projects');
  });

  it('ignores the fragment', () => {
    expect(currentGroupId(groups, '/approvals#top')).toBe('workspace');
  });
});

describe('resolveOpenGroup', () => {
  it('opens the group holding the page over any earlier choice', () => {
    expect(resolveOpenGroup(groups, '/finance/invoices', 'purchases')).toBe('sales');
    expect(resolveOpenGroup(groups, '/finance/invoices', null)).toBe('sales');
  });

  it('keeps the earlier choice on a page outside the menu', () => {
    expect(resolveOpenGroup(groups, '/account/security', 'purchases')).toBe('purchases');
    expect(resolveOpenGroup(groups, '/account/security', null)).toBeNull();
  });

  it('opens the first group when there is no valid choice', () => {
    expect(resolveOpenGroup(groups, '/account/security', undefined)).toBe('workspace');
    expect(resolveOpenGroup(groups, '/account/security', 'removed-module')).toBe('workspace');
  });

  it('keeps the choice until the menu has loaded', () => {
    expect(resolveOpenGroup([], '/finance/invoices', undefined)).toBeUndefined();
    expect(resolveOpenGroup([], '/finance/invoices', 'purchases')).toBe('purchases');
    expect(resolveOpenGroup([], '/finance/invoices', null)).toBeNull();
  });
});

describe('nextFocusIndex', () => {
  it('moves and wraps with the arrow keys', () => {
    expect(nextFocusIndex('ArrowDown', 0, 3)).toBe(1);
    expect(nextFocusIndex('ArrowDown', 2, 3)).toBe(0);
    expect(nextFocusIndex('ArrowUp', 1, 3)).toBe(0);
    expect(nextFocusIndex('ArrowUp', 0, 3)).toBe(2);
  });

  it('enters the list from outside it', () => {
    expect(nextFocusIndex('ArrowDown', -1, 3)).toBe(0);
    expect(nextFocusIndex('ArrowUp', -1, 3)).toBe(2);
  });

  it('jumps with Home and End, and leaves other keys alone', () => {
    expect(nextFocusIndex('Home', 2, 3)).toBe(0);
    expect(nextFocusIndex('End', 0, 3)).toBe(2);
    expect(nextFocusIndex('Enter', 1, 3)).toBeNull();
    expect(nextFocusIndex('ArrowDown', -1, 0)).toBeNull();
  });
});

describe('scrollDeltaToReveal', () => {
  it('stays put when the block already fits', () => {
    expect(scrollDeltaToReveal(0, 500, 100, 300)).toBe(0);
  });

  it('scrolls down just enough to show a block below the view', () => {
    expect(scrollDeltaToReveal(0, 500, 400, 600)).toBe(100);
  });

  it('scrolls up to show a block above the view', () => {
    expect(scrollDeltaToReveal(100, 600, 40, 200)).toBe(-60);
  });

  it('keeps the top visible when the block is taller than the view', () => {
    expect(scrollDeltaToReveal(0, 300, 200, 900)).toBe(200);
  });
});
