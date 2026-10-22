BEGIN TRANSACTION;
GO

ALTER TABLE [PaymentInstruments] ADD [BankAccountId] uniqueidentifier NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [BounceReason] nvarchar(400) NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [ClearedOn] date NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [DepositedOn] date NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [PartyName] nvarchar(400) NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [ReplacesInstrumentId] uniqueidentifier NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [SettlementId] uniqueidentifier NULL;
GO

CREATE TABLE [BankStatementLines] (
    [Id] uniqueidentifier NOT NULL,
    [AccountId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [Description] nvarchar(400) NOT NULL,
    [Reference] nvarchar(400) NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [MatchedJournalLineId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_BankStatementLines] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_BankStatementLines_MatchedJournalLineId] ON [BankStatementLines] ([MatchedJournalLineId]);
GO

CREATE INDEX [IX_BankStatementLines_OrgId_AccountId_Date] ON [BankStatementLines] ([OrgId], [AccountId], [Date]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022193656_ChequesAndBank', N'8.0.11');
GO

COMMIT;
GO

