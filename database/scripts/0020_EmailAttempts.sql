BEGIN TRANSACTION;
GO

ALTER TABLE [MessageLogs] ADD [Attempts] int NOT NULL DEFAULT 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023054857_EmailAttempts', N'8.0.11');
GO

COMMIT;
GO

