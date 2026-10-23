BEGIN TRANSACTION;
GO

CREATE TABLE [Budgets] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [FiscalYear] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    [LegalEntityId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Budgets] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [BudgetLines] (
    [Id] uniqueidentifier NOT NULL,
    [BudgetId] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [Month] int NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    CONSTRAINT [PK_BudgetLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BudgetLines_Budgets_BudgetId] FOREIGN KEY ([BudgetId]) REFERENCES [Budgets] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_BudgetLines_BudgetId_AccountId_Month] ON [BudgetLines] ([BudgetId], [AccountId], [Month]);
GO

CREATE INDEX [IX_Budgets_OrgId_LegalEntityId_FiscalYear] ON [Budgets] ([OrgId], [LegalEntityId], [FiscalYear]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023042754_Budgets', N'8.0.11');
GO

COMMIT;
GO

