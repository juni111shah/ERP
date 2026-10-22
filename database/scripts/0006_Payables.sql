BEGIN TRANSACTION;
GO

ALTER TABLE [Bills] ADD [PartyName] nvarchar(400) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022194501_Payables', N'8.0.11');
GO

COMMIT;
GO

