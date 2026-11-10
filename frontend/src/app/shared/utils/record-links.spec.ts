import { recordLink } from './record-links';

describe('recordLink', () => {
  it('links a known record type', () => {
    expect(recordLink('Invoice', 'x')).toEqual(['/finance/invoices', 'x']);
    expect(recordLink('Party', 'x')).toEqual(['/parties', 'x']);
  });

  it('tells money in from money out', () => {
    expect(recordLink('Settlement', 'x', '0')).toEqual(['/finance/receipts', 'x']);
    expect(recordLink('Settlement', 'x', 'Received')).toEqual(['/finance/receipts', 'x']);
    expect(recordLink('Settlement', 'x', '1')).toEqual(['/finance/payments', 'x']);
    expect(recordLink('Settlement', 'x', 'Issued')).toEqual(['/finance/payments', 'x']);
  });

  it('offers no link for a type without a page', () => {
    expect(recordLink('PlatformSetting', 'x')).toBeNull();
  });
});
