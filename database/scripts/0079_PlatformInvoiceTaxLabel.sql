BEGIN TRANSACTION;
GO

ALTER TABLE [SubscriptionInvoices] ADD [TaxLabel] nvarchar(40) NOT NULL DEFAULT N'';
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105185535_PlatformInvoiceTaxLabel', N'8.0.11');
GO

COMMIT;
GO

