BEGIN TRANSACTION;
GO

ALTER TABLE [Settlements] ADD [PartyName] nvarchar(400) NULL;
GO

ALTER TABLE [Invoices] ADD [PartyName] nvarchar(400) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022192452_Receivables', N'8.0.11');
GO

COMMIT;
GO

