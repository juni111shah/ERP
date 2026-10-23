BEGIN TRANSACTION;
GO

ALTER TABLE [Attachments] ADD [Category] nvarchar(50) NULL;
GO

ALTER TABLE [Attachments] ADD [Description] nvarchar(500) NULL;
GO

ALTER TABLE [Attachments] ADD [ExpiresOn] date NULL;
GO

CREATE INDEX [IX_Attachments_OrgId_ExpiresOn] ON [Attachments] ([OrgId], [ExpiresOn]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023050718_DocumentVault', N'8.0.11');
GO

COMMIT;
GO

