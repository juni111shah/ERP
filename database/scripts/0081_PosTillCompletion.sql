BEGIN TRANSACTION;
GO

ALTER TABLE [pos_Registers] ADD [CardAccountId] uniqueidentifier NULL;
GO

ALTER TABLE [pos_Registers] ADD [ExpenseAccountId] uniqueidentifier NULL;
GO

ALTER TABLE [pos_Registers] ADD [VarianceAccountId] uniqueidentifier NULL;
GO

ALTER TABLE [pos_Orders] ADD [InvoiceId] uniqueidentifier NULL;
GO

ALTER TABLE [pos_Orders] ADD [InvoiceNumber] nvarchar(40) NULL;
GO

ALTER TABLE [pos_Orders] ADD [ReturnedTotal] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [pos_OrderLines] ADD [ReturnedAmount] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [pos_OrderLines] ADD [ReturnedQuantity] decimal(18,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [pos_DrawerTransactions] ADD [AccountId] uniqueidentifier NULL;
GO

ALTER TABLE [pos_DrawerTransactions] ADD [PerformedByName] nvarchar(150) NULL;
GO

ALTER TABLE [Items] ADD [Barcode] nvarchar(64) NULL;
GO

CREATE TABLE [pos_Returns] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [OriginalOrderId] uniqueidentifier NOT NULL,
    [OriginalNumber] nvarchar(30) NOT NULL,
    [RegisterId] uniqueidentifier NOT NULL,
    [SessionId] uniqueidentifier NOT NULL,
    [CustomerPartyId] uniqueidentifier NULL,
    [CustomerName] nvarchar(200) NOT NULL,
    [ReturnDate] datetimeoffset NOT NULL,
    [SubTotal] decimal(19,4) NOT NULL,
    [TaxTotal] decimal(19,4) NOT NULL,
    [TotalAmount] decimal(19,4) NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [CashierUserId] uniqueidentifier NOT NULL,
    [CashierName] nvarchar(150) NOT NULL,
    [CreditNoteId] uniqueidentifier NULL,
    [CreditNoteNumber] nvarchar(40) NULL,
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
    CONSTRAINT [PK_pos_Returns] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [pos_ReturnLines] (
    [Id] uniqueidentifier NOT NULL,
    [PosReturnId] uniqueidentifier NOT NULL,
    [OrderLineId] uniqueidentifier NOT NULL,
    [ItemId] uniqueidentifier NULL,
    [ItemCode] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitPrice] decimal(19,4) NOT NULL,
    [DiscountPercent] decimal(5,2) NOT NULL,
    [TaxRate] decimal(5,2) NOT NULL,
    [NetAmount] decimal(19,4) NOT NULL,
    [TaxAmount] decimal(19,4) NOT NULL,
    [LineTotal] decimal(19,4) NOT NULL,
    [Restock] bit NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_pos_ReturnLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_pos_ReturnLines_pos_Returns_PosReturnId] FOREIGN KEY ([PosReturnId]) REFERENCES [pos_Returns] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [pos_ReturnRefunds] (
    [Id] uniqueidentifier NOT NULL,
    [PosReturnId] uniqueidentifier NOT NULL,
    [Method] int NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Reference] nvarchar(100) NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_pos_ReturnRefunds] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_pos_ReturnRefunds_pos_Returns_PosReturnId] FOREIGN KEY ([PosReturnId]) REFERENCES [pos_Returns] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX [IX_Items_OrgId_Barcode] ON [Items] ([OrgId], [Barcode]) WHERE [Barcode] IS NOT NULL AND [IsDeleted] = 0;
GO

CREATE INDEX [IX_pos_ReturnLines_OrgId_PosReturnId] ON [pos_ReturnLines] ([OrgId], [PosReturnId]);
GO

CREATE INDEX [IX_pos_ReturnLines_PosReturnId] ON [pos_ReturnLines] ([PosReturnId]);
GO

CREATE INDEX [IX_pos_ReturnRefunds_OrgId_PosReturnId] ON [pos_ReturnRefunds] ([OrgId], [PosReturnId]);
GO

CREATE INDEX [IX_pos_ReturnRefunds_PosReturnId] ON [pos_ReturnRefunds] ([PosReturnId]);
GO

CREATE UNIQUE INDEX [IX_pos_Returns_OrgId_Number] ON [pos_Returns] ([OrgId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_pos_Returns_OrgId_OriginalOrderId] ON [pos_Returns] ([OrgId], [OriginalOrderId]);
GO

CREATE INDEX [IX_pos_Returns_OrgId_SessionId] ON [pos_Returns] ([OrgId], [SessionId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105203822_PosTillCompletion', N'8.0.11');
GO

COMMIT;
GO

