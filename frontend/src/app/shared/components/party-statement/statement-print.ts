import { PartyLedger, PartyLedgerSide } from '@core/api/party-ledger.api';

/** What the printed statement shows besides the ledger itself. Texts are raw; the page escapes them. */
export interface StatementPrintInput {
  ledger: PartyLedger;
  side: PartyLedgerSide;
  /** The name the host passed, else the ledger's own. */
  partyName: string;
  orgName: string;
  currency: string;
  /** Text direction of the app ("ltr" or "rtl"). */
  dir: string;
  t: (key: string, fallback: string) => string;
  kindLabel: (kind: string) => string;
}

/** Escapes text for HTML. */
export const escapeHtml = (v: unknown) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);

/** The statement of account as a stand-alone printable page (A4, brand colour, opening and closing balance rows). */
export function statementPrintHtml(input: StatementPrintInput): string {
  const { ledger: d, side } = input;
  const esc = escapeHtml;
  const n = (v: number) => (v ?? 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const day = (v: string) => new Date(v + 'T00:00:00').toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
  const t = (k: string, f: string) => esc(input.t(k, f));
  const title = `${side === 'customer' ? t('statement.customerTitle', 'Customer Statement of Account') : t('statement.vendorTitle', 'Vendor Statement of Account')}`;
  const orgName = esc(input.orgName);
  const currency = esc(input.currency);
  const rows = d.lines.map((l) => `<tr>
      <td>${day(l.date)}</td>
      <td><span class="badge">${esc(input.kindLabel(l.kind))}</span></td>
      <td><strong>${esc(l.number)}</strong></td>
      <td>${esc(l.reference || '—')}</td>
      <td class="n">${l.debit ? n(l.debit) : '—'}</td>
      <td class="n">${l.credit ? n(l.credit) : '—'}</td>
      <td class="n balance"><strong>${n(l.balance)}</strong></td>
    </tr>`).join('');
  return `<!doctype html>
<html dir="${esc(input.dir)}">
<head>
  <meta charset="utf-8">
  <title>${title} · ${esc(input.partyName || d.partyName)}</title>
  <style>
    @page { size: A4 portrait; margin: 12mm 15mm; }
    * { box-sizing: border-box; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; margin: 0; padding: 20px; color: #0f172a; line-height: 1.45; font-size: 12px; }
    .sheet { max-width: 800px; margin: 0 auto; border-top: 5px solid #10955d; padding-top: 14px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; padding-bottom: 14px; border-bottom: 2px solid #10955d; }
    .org-name { font-size: 20px; font-weight: 800; color: #064e3b; margin: 0 0 4px; }
    .doc-title { font-size: 18px; font-weight: 800; color: #10955d; text-transform: uppercase; letter-spacing: 1px; margin: 0 0 4px; text-align: end; }
    .doc-dates { font-size: 11px; color: #64748b; text-align: end; }
    .party-block { margin: 18px 0; padding: 12px 16px; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; }
    .party-label { font-size: 10px; text-transform: uppercase; color: #64748b; font-weight: 700; letter-spacing: 0.8px; margin-bottom: 4px; }
    .party-name { font-size: 15px; font-weight: 700; color: #0f172a; }
    table { width: 100%; border-collapse: collapse; margin-top: 14px; }
    th { background: #f0fdf4; color: #064e3b; border-top: 1px solid #bbf7d0; border-bottom: 2px solid #10955d; padding: 8px 8px; font-size: 10.5px; text-transform: uppercase; text-align: start; }
    td { border-bottom: 1px solid #e2e8f0; padding: 8px 8px; text-align: start; vertical-align: middle; }
    .n { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .balance { color: #064e3b; }
    .badge { display: inline-block; padding: 1px 6px; background: #e2e8f0; border-radius: 4px; font-size: 10px; font-weight: 600; }
    tr.sum td { font-weight: 700; background: #f8fafc; }
    tr.closing td { background: #f0fdf4; border-top: 2px solid #10955d; border-bottom: 2px solid #10955d; font-size: 13px; font-weight: 800; color: #064e3b; }
    .footer { margin-top: 24px; padding-top: 12px; border-top: 1px solid #e2e8f0; font-size: 10.5px; color: #64748b; display: flex; justify-content: space-between; }
  </style>
</head>
<body>
  <div class="sheet">
    <div class="header">
      <div>
        <div class="org-name">${orgName}</div>
        <div style="font-size: 11px; color: #64748b;">Financial Statement of Account</div>
      </div>
      <div>
        <div class="doc-title">${title}</div>
        <div class="doc-dates">${t('statement.period', 'Statement Period')}: <strong>${day(d.from)} → ${day(d.to)}</strong></div>
        <div class="doc-dates">Currency: <strong>${currency}</strong></div>
      </div>
    </div>
    <div class="party-block">
      <div class="party-label">${side === 'customer' ? 'Account Holder / Customer' : 'Payee / Vendor'}</div>
      <div class="party-name">${esc(input.partyName || d.partyName || 'All Records')}</div>
    </div>
    <table>
      <thead>
        <tr>
          <th>${t('statement.date', 'Date')}</th>
          <th>${t('statement.type', 'Type')}</th>
          <th>${t('statement.number', 'Number')}</th>
          <th>${t('statement.reference', 'Reference')}</th>
          <th class="n">${t('statement.debit', 'Debit')}</th>
          <th class="n">${t('statement.credit', 'Credit')}</th>
          <th class="n">${t('statement.balance', 'Balance')}</th>
        </tr>
      </thead>
      <tbody>
        <tr class="sum">
          <td>${day(d.from)}</td>
          <td colspan="5"><strong>${t('statement.opening', 'Opening Balance')}</strong></td>
          <td class="n balance"><strong>${n(d.openingBalance)}</strong></td>
        </tr>
        ${rows}
        <tr class="closing">
          <td>${day(d.to)}</td>
          <td colspan="3"><strong>${t('statement.closing', 'Closing Balance')}</strong></td>
          <td class="n">${n(d.totalDebit)}</td>
          <td class="n">${n(d.totalCredit)}</td>
          <td class="n balance"><strong>${n(d.closingBalance)}</strong></td>
        </tr>
      </tbody>
    </table>
    <div class="footer">
      <div>This statement reflects posted transactions as of ${new Date().toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })}.</div>
      <div>${orgName} · HigherSquare 360 ERP</div>
    </div>
  </div>
</body>
</html>`;
}
