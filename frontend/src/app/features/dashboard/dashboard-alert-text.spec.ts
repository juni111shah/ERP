import { readableAlert } from './dashboard-alerts';

/** The API writes alerts as "1 document(s) have expired"; the Overview shows them as real English. */
describe('readableAlert', () => {
  it('uses the singular noun and verb for one', () => {
    expect(readableAlert('1 employee document(s) have expired.')).toBe('1 employee document has expired.');
    expect(readableAlert('1 document(s) expire within 30 days.')).toBe('1 document expires within 30 days.');
    expect(readableAlert('1 request(s) are waiting for your approval.')).toBe('1 request is waiting for your approval.');
    expect(readableAlert('1 cheque(s) worth 1,200.00 are due for deposit by 12 Nov.')).toBe('1 cheque worth 1,200.00 is due for deposit by 12 Nov.');
  });

  it('uses the plural noun for more than one', () => {
    expect(readableAlert('3 employee document(s) have expired.')).toBe('3 employee documents have expired.');
    expect(readableAlert('2 cheque(s) worth 1,200.00 are due for deposit by 12 Nov.')).toBe('2 cheques worth 1,200.00 are due for deposit by 12 Nov.');
  });

  it('leaves other messages alone', () => {
    expect(readableAlert('Cheque 1001 from Apex (500.00) bounced. Arrange a replacement.')).toBe('Cheque 1001 from Apex (500.00) bounced. Arrange a replacement.');
  });
});
