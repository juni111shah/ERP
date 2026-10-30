BEGIN TRANSACTION;
GO

DROP INDEX [IX_Accounts_OrgId_Code] ON [Accounts];
GO

ALTER TABLE [Parties] ADD [PartyGroupId] uniqueidentifier NULL;
GO

CREATE TABLE [PartyGroups] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [IsActive] bit NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_PartyGroups] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_Parties_OrgId_PartyGroupId] ON [Parties] ([OrgId], [PartyGroupId]);
GO

CREATE INDEX [IX_Parties_PartyGroupId] ON [Parties] ([PartyGroupId]);
GO

CREATE UNIQUE INDEX [IX_Accounts_OrgId_Code] ON [Accounts] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [IX_PartyGroups_OrgId_Code] ON [PartyGroups] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO

ALTER TABLE [Parties] ADD CONSTRAINT [FK_Parties_PartyGroups_PartyGroupId] FOREIGN KEY ([PartyGroupId]) REFERENCES [PartyGroups] ([Id]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029203606_PartyGroupsAndCodeReuse', N'8.0.11');
GO

COMMIT;
GO

