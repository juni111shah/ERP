import { isFirstPaymentMoment } from './celebrate';

describe('celebration is rare', () => {
  const workspace = 'spec-workspace';
  const key = `erp.celebrated.firstPayment:${workspace}`;
  beforeEach(() => localStorage.removeItem(key));
  afterEach(() => localStorage.removeItem(key));

  it('happens for the first payment applied to an invoice, once', () => {
    expect(isFirstPaymentMoment(workspace, 1, true)).toBeTrue();
    expect(isFirstPaymentMoment(workspace, 1, true)).toBeFalse();
  });

  it('does not happen for later payments or money not yet applied', () => {
    expect(isFirstPaymentMoment(workspace, 2, true)).toBeFalse();
    expect(isFirstPaymentMoment(workspace, 1, false)).toBeFalse();
  });
});
