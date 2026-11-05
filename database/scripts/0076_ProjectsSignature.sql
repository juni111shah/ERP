BEGIN TRANSACTION;
GO

ALTER TABLE [prj_Acceptances] ADD [EvidenceHash] nvarchar(64) NULL;
GO

ALTER TABLE [prj_Acceptances] ADD [SignatureImage] nvarchar(400) NULL;
GO

ALTER TABLE [prj_Acceptances] ADD [SignedIp] nvarchar(64) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105105857_ProjectsSignature', N'8.0.11');
GO

COMMIT;
GO

