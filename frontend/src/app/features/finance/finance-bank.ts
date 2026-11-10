/** Bank statement import: the pasted "date, description, reference, amount" lines read into statement lines. */

/** A bank statement line as the import endpoint takes it. */
export interface StatementImportLine { date: string; description: string; reference: string | null; amount: number; }

/** The lines read, or (`bad`) the first line that could not be read. */
export interface StatementParse { lines: StatementImportLine[]; bad: string | null; }

/**
 * Reads one statement line per row ("2026-05-03, Deposit, D1, 10000"); blank rows are skipped.
 * A row needs an ISO date, a description and a non-zero amount; the reference is optional.
 */
export function parseStatementLines(text: string): StatementParse {
  const lines: StatementImportLine[] = [];
  for (const raw of text.split('\n').map((l) => l.trim()).filter(Boolean)) {
    const [date, description, reference, amount] = raw.split(',').map((p) => p.trim());
    const value = Number(amount);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date ?? '') || !description || !Number.isFinite(value) || value === 0) return { lines: [], bad: raw };
    lines.push({ date, description, reference: reference || null, amount: value });
  }
  return { lines, bad: null };
}
