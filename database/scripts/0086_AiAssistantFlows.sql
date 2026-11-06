BEGIN TRANSACTION;
GO

ALTER TABLE [PlatformSettings] ADD [WorkspaceAssistantDailyMessagesPerClient] int NOT NULL DEFAULT 200;
GO

ALTER TABLE [BrandMessages] ADD [Helpful] bit NULL;
GO

ALTER TABLE [ast_PendingActions] ADD [Language] nvarchar(5) NOT NULL DEFAULT N'';
GO

ALTER TABLE [ast_PendingActions] ADD [LegalEntityId] uniqueidentifier NULL;
GO

ALTER TABLE [ast_Messages] ADD [Helpful] bit NULL;
GO

ALTER TABLE [ast_Messages] ADD [IsNote] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [AiProviders] ADD [ConsecutiveFailures] int NOT NULL DEFAULT 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106113659_AiAssistantFlows', N'8.0.11');
GO

COMMIT;
GO


