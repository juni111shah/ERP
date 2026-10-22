BEGIN TRANSACTION;
GO

CREATE TABLE [re_Acquisitions] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NULL,
    [PropertyId] uniqueidentifier NOT NULL,
    [SellerName] nvarchar(200) NOT NULL,
    [PurchaseDate] date NOT NULL,
    [ContractReference] nvarchar(100) NULL,
    [PurchasePrice] decimal(19,4) NOT NULL,
    [DownPayment] decimal(19,4) NOT NULL,
    [TransferFees] decimal(19,4) NOT NULL,
    [OtherCosts] decimal(19,4) NOT NULL,
    [PayFromAccountId] uniqueidentifier NULL,
    [Notes] nvarchar(1000) NULL,
    [CurrencyCode] nchar(3) NOT NULL,
    [Status] int NOT NULL,
    [CompletedAt] datetime2 NULL,
    [JournalEntryId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_re_Acquisitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_Acquisitions_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [re_FundingSources] (
    [Id] uniqueidentifier NOT NULL,
    [AcquisitionId] uniqueidentifier NOT NULL,
    [Type] int NOT NULL,
    [Lender] nvarchar(200) NOT NULL,
    [Principal] decimal(19,4) NOT NULL,
    [AnnualRatePercent] decimal(9,4) NOT NULL,
    [TermMonths] int NOT NULL,
    [FirstDueDate] date NOT NULL,
    [MonthlyPayment] decimal(19,4) NOT NULL,
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
    CONSTRAINT [PK_re_FundingSources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_FundingSources_re_Acquisitions_AcquisitionId] FOREIGN KEY ([AcquisitionId]) REFERENCES [re_Acquisitions] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [re_FundingInstalments] (
    [Id] uniqueidentifier NOT NULL,
    [FundingSourceId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [DueDate] date NOT NULL,
    [Principal] decimal(19,4) NOT NULL,
    [Interest] decimal(19,4) NOT NULL,
    [Total] decimal(19,4) NOT NULL,
    [BalanceAfter] decimal(19,4) NOT NULL,
    [Status] int NOT NULL,
    [PaidOn] date NULL,
    [JournalEntryId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_re_FundingInstalments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_FundingInstalments_re_FundingSources_FundingSourceId] FOREIGN KEY ([FundingSourceId]) REFERENCES [re_FundingSources] ([Id]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_re_Acquisitions_OrgId_LegalEntityId_Number] ON [re_Acquisitions] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0 AND [Number] IS NOT NULL;
GO

CREATE INDEX [IX_re_Acquisitions_OrgId_PropertyId] ON [re_Acquisitions] ([OrgId], [PropertyId]);
GO

CREATE INDEX [IX_re_Acquisitions_PropertyId] ON [re_Acquisitions] ([PropertyId]);
GO

CREATE INDEX [IX_re_FundingInstalments_FundingSourceId] ON [re_FundingInstalments] ([FundingSourceId]);
GO

CREATE UNIQUE INDEX [IX_re_FundingInstalments_OrgId_FundingSourceId_Sequence] ON [re_FundingInstalments] ([OrgId], [FundingSourceId], [Sequence]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_re_FundingInstalments_OrgId_Status_DueDate] ON [re_FundingInstalments] ([OrgId], [Status], [DueDate]);
GO

CREATE INDEX [IX_re_FundingSources_AcquisitionId] ON [re_FundingSources] ([AcquisitionId]);
GO

CREATE INDEX [IX_re_FundingSources_OrgId_AcquisitionId] ON [re_FundingSources] ([OrgId], [AcquisitionId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022201459_Acquisitions', N'8.0.11');
GO

COMMIT;
GO

