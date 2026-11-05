BEGIN TRANSACTION;
GO

ALTER TABLE [prj_Timesheets] ADD [WipReleaseJournalId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Expenses] ADD [WipReleaseJournalId] uniqueidentifier NULL;
GO

CREATE TABLE [prj_Acceptances] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [MilestoneId] uniqueidentifier NULL,
    [Title] nvarchar(200) NOT NULL,
    [Status] int NOT NULL,
    [RequestedAt] datetime2 NOT NULL,
    [RequestedByName] nvarchar(150) NULL,
    [SignedByName] nvarchar(150) NULL,
    [Signature] nvarchar(150) NULL,
    [SignedAt] datetime2 NULL,
    [Comment] nvarchar(1000) NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Acceptances] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_prj_Acceptances_OrgId_ProjectId_Status] ON [prj_Acceptances] ([OrgId], [ProjectId], [Status]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105104451_ProjectsPortal', N'8.0.11');
GO

COMMIT;
GO

