BEGIN TRANSACTION;
GO

ALTER TABLE [prj_Tasks] ADD [BaselineEnd] date NULL;
GO

ALTER TABLE [prj_Tasks] ADD [BaselineStart] date NULL;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[prj_Acceptances]') AND [c].[name] = N'SignatureImage');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [prj_Acceptances] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [prj_Acceptances] ALTER COLUMN [SignatureImage] nvarchar(max) NULL;
GO

CREATE TABLE [prj_BudgetLines] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Category] nvarchar(60) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Hours] decimal(9,2) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_BudgetLines] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_prj_BudgetLines_OrgId_ProjectId_Category] ON [prj_BudgetLines] ([OrgId], [ProjectId], [Category]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105120054_ProjectsWorkspace', N'8.0.11');
GO

COMMIT;
GO

