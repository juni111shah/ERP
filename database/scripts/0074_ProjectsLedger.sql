BEGIN TRANSACTION;
GO

ALTER TABLE [prj_Timesheets] ADD [CostJournalId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Expenses] ADD [CostJournalId] uniqueidentifier NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105103353_ProjectsLedger', N'8.0.11');
GO

COMMIT;
GO

