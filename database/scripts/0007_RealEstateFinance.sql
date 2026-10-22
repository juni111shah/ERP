BEGIN TRANSACTION;
GO

ALTER TABLE [re_RentInstalments] ADD [CreditNoteId] uniqueidentifier NULL;
GO

ALTER TABLE [re_BrokerageDeals] ADD [AgentPaidOn] date NULL;
GO

ALTER TABLE [re_BrokerageDeals] ADD [AgentPayoutJournalId] uniqueidentifier NULL;
GO

ALTER TABLE [re_BrokerageDeals] ADD [CommissionInvoiceId] uniqueidentifier NULL;
GO

CREATE TABLE [re_DepositTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [LeaseId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [Kind] int NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Description] nvarchar(300) NULL,
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
    CONSTRAINT [PK_re_DepositTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_DepositTransactions_re_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [re_Leases] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_re_DepositTransactions_LeaseId] ON [re_DepositTransactions] ([LeaseId]);
GO

CREATE INDEX [IX_re_DepositTransactions_OrgId_LeaseId_Date] ON [re_DepositTransactions] ([OrgId], [LeaseId], [Date]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022200718_RealEstateFinance', N'8.0.11');
GO

COMMIT;
GO

