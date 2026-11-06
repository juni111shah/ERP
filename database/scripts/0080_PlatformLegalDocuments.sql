BEGIN TRANSACTION;
GO

ALTER TABLE [SignupRequests] ADD [TermsAcceptedAt] datetime2 NULL;
GO

ALTER TABLE [PlatformSettings] ADD [LegalUpdatedAt] datetime2 NULL;
GO

ALTER TABLE [PlatformSettings] ADD [PrivacyText] nvarchar(max) NULL;
GO

ALTER TABLE [PlatformSettings] ADD [TermsText] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105192006_PlatformLegalDocuments', N'8.0.11');
GO

COMMIT;
GO

