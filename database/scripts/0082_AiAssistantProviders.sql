Build started...
Build succeeded.
BEGIN TRANSACTION;
GO

ALTER TABLE [PlatformSettings] ADD [BrandAssistantEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [PlatformSettings] ADD [OwnerUserId] uniqueidentifier NULL;
GO

ALTER TABLE [PlatformSettings] ADD [WorkspaceAssistantEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

CREATE TABLE [AiProviders] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(80) NOT NULL,
    [BaseUrl] nvarchar(300) NOT NULL,
    [Model] nvarchar(150) NOT NULL,
    [EncryptedApiKey] nvarchar(4000) NOT NULL,
    [ApiKeyHint] nvarchar(4) NOT NULL,
    [UseForBrand] bit NOT NULL,
    [UseForWorkspace] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [LastTestedAt] datetime2 NULL,
    [LastTestOk] bit NULL,
    [LastTestMessage] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_AiProviders] PRIMARY KEY ([Id])
);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106062143_AiAssistantProviders', N'8.0.11');
GO

COMMIT;
GO


