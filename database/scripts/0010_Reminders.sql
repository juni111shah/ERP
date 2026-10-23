BEGIN TRANSACTION;
GO

CREATE TABLE [MessageLogs] (
    [Id] uniqueidentifier NOT NULL,
    [Channel] nvarchar(30) NOT NULL,
    [Recipient] nvarchar(400) NOT NULL,
    [Subject] nvarchar(400) NOT NULL,
    [Body] nvarchar(2000) NULL,
    [Link] nvarchar(400) NULL,
    [DedupeKey] nvarchar(300) NOT NULL,
    [Status] int NOT NULL,
    [Error] nvarchar(400) NULL,
    [SentAt] datetime2 NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_MessageLogs] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_MessageLogs_OrgId_DedupeKey] ON [MessageLogs] ([OrgId], [DedupeKey]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023035937_Reminders', N'8.0.11');
GO

COMMIT;
GO

