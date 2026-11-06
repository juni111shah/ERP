BEGIN TRANSACTION;
GO

ALTER TABLE [PlatformSettings] ADD [AboutText] nvarchar(2000) NULL;
GO

CREATE TABLE [ast_Conversations] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Title] nvarchar(120) NOT NULL,
    [StartedAt] datetime2 NOT NULL,
    [LastMessageAt] datetime2 NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ast_Conversations] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ast_Messages] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [At] datetime2 NOT NULL,
    [FromUser] bit NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ast_Messages] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ast_PendingActions] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [ActionKey] nvarchar(60) NOT NULL,
    [ArgumentsJson] nvarchar(max) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [LinesJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [Message] nvarchar(500) NULL,
    [Link] nvarchar(300) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ast_PendingActions] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [BrandConversations] (
    [Id] uniqueidentifier NOT NULL,
    [StartedAt] datetime2 NOT NULL,
    [LastMessageAt] datetime2 NOT NULL,
    [VisitorMessages] int NOT NULL,
    [NeedsReview] bit NOT NULL,
    CONSTRAINT [PK_BrandConversations] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [BrandLeads] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Phone] nvarchar(40) NULL,
    [Company] nvarchar(200) NULL,
    [Need] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_BrandLeads] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [BrandMessages] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [At] datetime2 NOT NULL,
    [FromVisitor] bit NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_BrandMessages] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_ast_Conversations_OrgId_UserId_LastMessageAt] ON [ast_Conversations] ([OrgId], [UserId], [LastMessageAt]);
GO

CREATE INDEX [IX_ast_Messages_OrgId_ConversationId_At] ON [ast_Messages] ([OrgId], [ConversationId], [At]);
GO

CREATE INDEX [IX_ast_Messages_OrgId_UserId_At] ON [ast_Messages] ([OrgId], [UserId], [At]);
GO

CREATE INDEX [IX_ast_PendingActions_OrgId_ConversationId] ON [ast_PendingActions] ([OrgId], [ConversationId]);
GO

CREATE INDEX [IX_BrandConversations_LastMessageAt] ON [BrandConversations] ([LastMessageAt]);
GO

CREATE INDEX [IX_BrandLeads_CreatedAt] ON [BrandLeads] ([CreatedAt]);
GO

CREATE INDEX [IX_BrandMessages_ConversationId_At] ON [BrandMessages] ([ConversationId], [At]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106075512_AiAssistantBrandAndWorkspace', N'8.0.11');
GO

COMMIT;
GO


