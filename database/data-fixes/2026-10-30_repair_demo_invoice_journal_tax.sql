-- Data repair (NOT a schema change, no EF migration): demo/seed invoice journals "JV-BASE-nnn-INV".
-- The old seeder debited receivables for the invoice total but credited income only for the net amount, so the output tax was never booked
-- and each of those entries was out of balance by the invoice's tax. This adds the missing credit (debit for a credit note) on the
-- workspace's 'tax.output' account. Safe to run twice: it only touches entries that are still unbalanced.
-- It aborts without changing anything if an unbalanced entry's gap is not exactly its invoice's tax.
-- Run per database after taking a backup, for example:  sqlcmd -S localhost -E -C -d ERP_Dev -b -i <this file>

SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @fix TABLE (JournalId uniqueidentifier, OrgId uniqueidentifier, TaxAccountId uniqueidentifier, Gap decimal(19,4), InvoiceTax decimal(19,4), IsCreditNote bit, NextSeq int);

INSERT INTO @fix
SELECT j.Id, j.OrgId, a.Id, SUM(l.Debit) - SUM(l.Credit), i.Tax, i.IsCreditNote, MAX(l.Seq) + 1
FROM JournalEntries j
JOIN JournalLines l ON l.JournalEntryId = j.Id
JOIN Invoices i ON i.Id = j.SourceId
JOIN Accounts a ON a.OrgId = j.OrgId AND a.SystemRole = 'tax.output' AND a.IsActive = 1
WHERE j.SourceType = 'invoice' AND j.Number LIKE 'JV-BASE-%-INV'
GROUP BY j.Id, j.OrgId, a.Id, i.Tax, i.IsCreditNote
HAVING SUM(l.Debit) <> SUM(l.Credit);

IF EXISTS (SELECT 1 FROM @fix WHERE ABS(Gap) <> InvoiceTax)
BEGIN
    RAISERROR('An unbalanced entry has a gap that is not its invoice tax; nothing was changed.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Invoice: gap is debit-heavy, add the tax credit. Credit note: the opposite.
INSERT INTO JournalLines (Id, JournalEntryId, Seq, AccountId, Debit, Credit, Description)
SELECT NEWID(), JournalId, NextSeq, TaxAccountId,
       CASE WHEN Gap < 0 THEN -Gap ELSE 0 END,
       CASE WHEN Gap > 0 THEN Gap ELSE 0 END,
       N'Output tax (repair of demo journal)'
FROM @fix;

SELECT COUNT(*) AS repaired FROM @fix;
SELECT COUNT(*) AS still_unbalanced FROM (SELECT JournalEntryId FROM JournalLines GROUP BY JournalEntryId HAVING SUM(Debit) <> SUM(Credit)) x;

COMMIT TRANSACTION;
