BEGIN TRANSACTION;
GO

ALTER TABLE [SubscriptionPlans] ADD [AssistantMonthlyMessages] int NULL;
GO

CREATE TABLE [ast_MessageCounts] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Day] date NOT NULL,
    [Count] int NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ast_MessageCounts] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_ast_MessageCounts_OrgId_Day] ON [ast_MessageCounts] ([OrgId], [Day]);
GO

CREATE INDEX [IX_ast_MessageCounts_OrgId_UserId_Day] ON [ast_MessageCounts] ([OrgId], [UserId], [Day]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106091621_AiAssistantAllowance', N'8.0.11');
GO

COMMIT;
GO


