BEGIN TRANSACTION;
GO

ALTER TABLE [JournalEntries] ADD [Amount] decimal(18,2) NOT NULL DEFAULT 0.0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022191803_LedgerJournalAmount', N'8.0.11');
GO

COMMIT;
GO

