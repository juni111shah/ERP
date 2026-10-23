BEGIN TRANSACTION;
GO

ALTER TABLE [re_Properties] ADD [ManagementFeePercent] decimal(9,4) NOT NULL DEFAULT 0.0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023050015_OwnerStatements', N'8.0.11');
GO

COMMIT;
GO

