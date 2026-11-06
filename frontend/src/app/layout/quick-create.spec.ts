import { QUICK_CREATE, visibleQuickCreate } from './quick-create';

describe('Quick Create menu', () => {
  const labels = (groups: ReturnType<typeof visibleQuickCreate>) => groups.flatMap((g) => g.entries.map((e) => e.label));

  it('shows only entries whose module is on and whose permission is held', () => {
    const groups = visibleQuickCreate((m) => m === 'sales', (p) => p.startsWith('finance.') || p.startsWith('sales.'));
    const shown = labels(groups);
    expect(shown).toContain('Invoice');
    expect(shown).toContain('Quotation');
    expect(shown).not.toContain('Purchase Order');
    expect(shown).not.toContain('POS Terminal');
    expect(shown).not.toContain('Customer');
  });

  it('drops a group when nothing in it is allowed', () => {
    const groups = visibleQuickCreate(() => false, (p) => p === 'core.items.write');
    expect(groups.map((g) => g.title)).toEqual(['Catalog']);
    expect(labels(groups)).toEqual(['Item / Product']);
  });

  it('names no business-specific destination', () => {
    const links = QUICK_CREATE.flatMap((g) => g.entries.map((e) => e.link));
    expect(links.some((l) => /real-estate|manufacturing|workorders|leases/.test(l))).toBeFalse();
  });
});
