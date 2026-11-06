BEGIN TRANSACTION;
GO

ALTER TABLE [PlatformSettings] ADD [BrandAssistantDailyTokens] int NOT NULL DEFAULT 150000;
GO

ALTER TABLE [PlatformSettings] ADD [WorkspaceAssistantDailyTokens] int NOT NULL DEFAULT 50000;
GO

CREATE TABLE [AssistantUsageDays] (
    [Id] uniqueidentifier NOT NULL,
    [Day] date NOT NULL,
    [Bot] nvarchar(20) NOT NULL,
    [ProviderId] uniqueidentifier NOT NULL,
    [ProviderName] nvarchar(80) NOT NULL,
    [Requests] int NOT NULL,
    [Failures] int NOT NULL,
    [InputTokens] bigint NOT NULL,
    [OutputTokens] bigint NOT NULL,
    [LastFailure] nvarchar(200) NULL,
    CONSTRAINT [PK_AssistantUsageDays] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_AssistantUsageDays_Day_Bot_ProviderId] ON [AssistantUsageDays] ([Day], [Bot], [ProviderId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106065427_AiAssistantUsage', N'8.0.11');
GO

COMMIT;
GO


