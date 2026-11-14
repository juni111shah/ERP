BEGIN TRANSACTION;
GO

ALTER TABLE [Users] ADD [PlatformRole] nvarchar(40) NOT NULL DEFAULT N'Support';
GO

-- The platform team had one level until now: everyone already on it keeps what they could do.
UPDATE [Users] SET [PlatformRole] = N'Manager' WHERE [IsPlatformAdmin] = 1;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261114070000_PlatformTeamRoles', N'8.0.11');
GO

COMMIT;
GO
