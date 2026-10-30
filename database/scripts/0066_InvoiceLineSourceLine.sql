BEGIN TRANSACTION;
GO

ALTER TABLE [InvoiceLine] ADD [SourceLineId] uniqueidentifier NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030114901_InvoiceLineSourceLine', N'8.0.11');
GO

COMMIT;
GO

