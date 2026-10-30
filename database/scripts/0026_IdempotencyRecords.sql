BEGIN TRANSACTION;
GO

CREATE TABLE [IdempotencyRecords] (
    [Id] uniqueidentifier NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Key] nvarchar(100) NOT NULL,
    [Route] nvarchar(300) NOT NULL,
    [RequestHash] nvarchar(64) NOT NULL,
    [StatusCode] int NOT NULL,
    [ContentType] nvarchar(100) NULL,
    [ResponseBody] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_IdempotencyRecords_CreatedAt] ON [IdempotencyRecords] ([CreatedAt]);
GO

CREATE UNIQUE INDEX [IX_IdempotencyRecords_OrgId_UserId_Route_Key] ON [IdempotencyRecords] ([OrgId], [UserId], [Route], [Key]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029235542_IdempotencyRecords', N'8.0.11');
GO

COMMIT;
GO

