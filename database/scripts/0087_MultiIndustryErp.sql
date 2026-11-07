BEGIN TRANSACTION;
GO

ALTER TABLE [svc_TimeEntries] ADD [InvoiceId] uniqueidentifier NULL;
GO

ALTER TABLE [sls_OrderLines] ADD [AutoDeliveredQuantity] decimal(18,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [sls_OrderLines] ADD [ReturnedQuantity] decimal(18,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [Settlements] ADD [ApprovalRequestId] uniqueidentifier NULL;
GO

ALTER TABLE [Settlements] ADD [PendingInput] nvarchar(max) NULL;
GO

ALTER TABLE [Settlements] ADD [VoidPostedOn] date NULL;
GO

ALTER TABLE [re_RentInstalments] ADD [CreditedToOwner] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [re_Properties] ADD [CreditRentToOwner] bit NULL;
GO

ALTER TABLE [pur_Requisitions] ADD [ApprovalRequestId] uniqueidentifier NULL;
GO

ALTER TABLE [pur_Orders] ADD [ApprovalRequestId] uniqueidentifier NULL;
GO

ALTER TABLE [pur_OrderLines] ADD [ReturnedQuantity] decimal(18,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [pur_GoodsReceipts] ADD [WarehouseId] uniqueidentifier NULL;
GO

ALTER TABLE [pur_GoodsReceiptLines] ADD [NoStockMoved] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [prj_Projects] ADD [ContractValue] decimal(19,4) NULL;
GO

ALTER TABLE [pos_Orders] ADD [PromoCode] nvarchar(50) NULL;
GO

ALTER TABLE [pos_Orders] ADD [PromoDiscount] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [pos_Orders] ADD [PromotionId] uniqueidentifier NULL;
GO

ALTER TABLE [pos_OrderLines] ADD [PromoDiscount] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [Parties] ADD [CreditLimit] decimal(18,2) NULL;
GO

ALTER TABLE [Organizations] ADD [IsDemo] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [mfg_WorkOrders] ADD [ConversionCost] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [mfg_WorkOrders] ADD [MaterialCost] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [mfg_WorkOrders] ADD [ReversalReason] nvarchar(500) NULL;
GO

ALTER TABLE [mfg_WorkOrders] ADD [ReversedAt] datetimeoffset NULL;
GO

ALTER TABLE [mfg_WorkOrders] ADD [ReversedByUserId] uniqueidentifier NULL;
GO

ALTER TABLE [mfg_WorkOrders] ADD [ScrapCost] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [mfg_BomComponents] ADD [UseCurrentCost] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [JournalEntries] ADD [ApprovalRequestId] uniqueidentifier NULL;
GO

ALTER TABLE [Items] ADD [Category] nvarchar(100) NULL;
GO

ALTER TABLE [Invoices] ADD [VoidPostedOn] date NULL;
GO

ALTER TABLE [inv_StockMovements] ADD [ReturnOfReference] nvarchar(100) NULL;
GO

ALTER TABLE [dst_VanSalesTrips] ADD [InvoiceId] uniqueidentifier NULL;
GO

ALTER TABLE [dst_VanSalesTrips] ADD [InvoiceNumber] nvarchar(50) NULL;
GO

ALTER TABLE [BusinessUnits] ADD [Kind] nvarchar(30) NOT NULL DEFAULT N'branch';
GO

ALTER TABLE [Bills] ADD [VoidPostedOn] date NULL;
GO

ALTER TABLE [ApprovalPolicies] ADD [Level] int NOT NULL DEFAULT 1;
GO

CREATE TABLE [ApprovalSteps] (
    [Id] uniqueidentifier NOT NULL,
    [ApprovalRequestId] uniqueidentifier NOT NULL,
    [Level] int NOT NULL,
    [ApproverPermission] nvarchar(100) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [DecidedBy] uniqueidentifier NULL,
    [DecidedAt] datetime2 NULL,
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
    CONSTRAINT [PK_ApprovalSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ApprovalSteps_ApprovalRequests_ApprovalRequestId] FOREIGN KEY ([ApprovalRequestId]) REFERENCES [ApprovalRequests] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ast_Categories] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(30) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [LifeMonths] int NOT NULL,
    [Method] nvarchar(30) NOT NULL,
    [DecliningRatePercent] decimal(7,4) NULL,
    [AssetAccountId] uniqueidentifier NOT NULL,
    [AccumulatedDepreciationAccountId] uniqueidentifier NOT NULL,
    [DepreciationExpenseAccountId] uniqueidentifier NOT NULL,
    [DisposalGainLossAccountId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_ast_Categories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ast_Categories_Accounts_AccumulatedDepreciationAccountId] FOREIGN KEY ([AccumulatedDepreciationAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_ast_Categories_Accounts_AssetAccountId] FOREIGN KEY ([AssetAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_ast_Categories_Accounts_DepreciationExpenseAccountId] FOREIGN KEY ([DepreciationExpenseAccountId]) REFERENCES [Accounts] ([Id]),
    CONSTRAINT [FK_ast_Categories_Accounts_DisposalGainLossAccountId] FOREIGN KEY ([DisposalGainLossAccountId]) REFERENCES [Accounts] ([Id])
);
GO

CREATE TABLE [ast_DepreciationRuns] (
    [Id] uniqueidentifier NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [PeriodEnd] date NOT NULL,
    [JournalEntryId] uniqueidentifier NULL,
    [Total] decimal(18,2) NOT NULL,
    [AssetCount] int NOT NULL,
    [RunAt] datetime2 NOT NULL,
    [RunBy] uniqueidentifier NULL,
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
    CONSTRAINT [PK_ast_DepreciationRuns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ast_DepreciationRuns_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id])
);
GO

CREATE TABLE [FiscalYearCloses] (
    [Id] uniqueidentifier NOT NULL,
    [FiscalYear] int NOT NULL,
    [YearStart] date NOT NULL,
    [YearEnd] date NOT NULL,
    [JournalEntryId] uniqueidentifier NULL,
    [NetResult] decimal(18,2) NOT NULL,
    [PreviousBooksLockedTo] date NULL,
    [ClosedAt] datetime2 NOT NULL,
    [ClosedBy] uniqueidentifier NULL,
    [ReopenedAt] datetime2 NULL,
    [ReopenedBy] uniqueidentifier NULL,
    [ReopenReason] nvarchar(500) NULL,
    [ReversalJournalId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_FiscalYearCloses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FiscalYearCloses_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]),
    CONSTRAINT [FK_FiscalYearCloses_LegalEntities_LegalEntityId] FOREIGN KEY ([LegalEntityId]) REFERENCES [LegalEntities] ([Id])
);
GO

CREATE TABLE [ind_Records] (
    [Id] uniqueidentifier NOT NULL,
    [ModuleKey] nvarchar(40) NOT NULL,
    [RecordType] nvarchar(40) NOT NULL,
    [Number] nvarchar(50) NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [PartyId] uniqueidentifier NULL,
    [PartyName] nvarchar(300) NULL,
    [ParentId] uniqueidentifier NULL,
    [ParentTitle] nvarchar(300) NULL,
    [BusinessUnitId] uniqueidentifier NULL,
    [RecordDate] date NOT NULL,
    [DueDate] date NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Data] nvarchar(400) NOT NULL,
    [Notes] nvarchar(4000) NULL,
    [InvoiceId] uniqueidentifier NULL,
    [BillId] uniqueidentifier NULL,
    [ConsumedOn] date NULL,
    [CustomFields] nvarchar(max) NULL,
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
    CONSTRAINT [PK_ind_Records] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ind_Records_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
);
GO

CREATE TABLE [mfg_WorkOrderMaterialIssues] (
    [Id] uniqueidentifier NOT NULL,
    [WorkOrderId] uniqueidentifier NOT NULL,
    [ItemId] uniqueidentifier NOT NULL,
    [ItemCode] nvarchar(50) NOT NULL,
    [ItemName] nvarchar(200) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [UnitCost] decimal(19,4) NOT NULL,
    [Value] decimal(19,4) NOT NULL,
    [IssueDate] date NOT NULL,
    [Backflushed] bit NOT NULL,
    [Notes] nvarchar(500) NULL,
    [ReversedAt] datetimeoffset NULL,
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
    CONSTRAINT [PK_mfg_WorkOrderMaterialIssues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_mfg_WorkOrderMaterialIssues_mfg_WorkOrders_WorkOrderId] FOREIGN KEY ([WorkOrderId]) REFERENCES [mfg_WorkOrders] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [pur_OrderBillLines] (
    [Id] uniqueidentifier NOT NULL,
    [OrderId] uniqueidentifier NOT NULL,
    [OrderLineId] uniqueidentifier NOT NULL,
    [BillId] uniqueidentifier NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [ClearingQuantity] decimal(18,4) NOT NULL,
    [UnitPrice] decimal(19,4) NOT NULL,
    [TaxRate] decimal(5,2) NOT NULL,
    [TaxRecoverable] bit NOT NULL,
    [ReturnedQuantity] decimal(18,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_pur_OrderBillLines] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [pur_Returns] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [PurchaseOrderId] uniqueidentifier NULL,
    [PurchaseOrderNumber] nvarchar(30) NULL,
    [BillId] uniqueidentifier NULL,
    [VendorPartyId] uniqueidentifier NOT NULL,
    [VendorName] nvarchar(200) NOT NULL,
    [ReturnDate] date NOT NULL,
    [Status] int NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [WarehouseId] uniqueidentifier NULL,
    [SubTotal] decimal(19,4) NOT NULL,
    [TaxTotal] decimal(19,4) NOT NULL,
    [TotalAmount] decimal(19,4) NOT NULL,
    [StockValue] decimal(19,4) NOT NULL,
    [JournalEntryId] uniqueidentifier NULL,
    [VoidReason] nvarchar(500) NULL,
    [VoidedAt] datetime2 NULL,
    [VoidedBy] uniqueidentifier NULL,
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
    CONSTRAINT [PK_pur_Returns] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [re_OwnerPayouts] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [OwnerPartyId] uniqueidentifier NOT NULL,
    [PeriodFrom] date NOT NULL,
    [PeriodTo] date NOT NULL,
    [Date] date NOT NULL,
    [Method] int NOT NULL,
    [Status] int NOT NULL,
    [RentCollected] decimal(19,4) NOT NULL,
    [ManagementFee] decimal(19,4) NOT NULL,
    [MaintenanceCosts] decimal(19,4) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [BankAccountId] uniqueidentifier NULL,
    [BillId] uniqueidentifier NULL,
    [JournalEntryId] uniqueidentifier NULL,
    [Notes] nvarchar(500) NULL,
    [VoidReason] nvarchar(500) NULL,
    [VoidedAt] datetime2 NULL,
    [VoidedBy] uniqueidentifier NULL,
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
    CONSTRAINT [PK_re_OwnerPayouts] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [sls_Returns] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [SalesOrderId] uniqueidentifier NULL,
    [OrderNumber] nvarchar(30) NULL,
    [InvoiceId] uniqueidentifier NULL,
    [InvoiceNumber] nvarchar(50) NULL,
    [CustomerPartyId] uniqueidentifier NOT NULL,
    [CustomerName] nvarchar(200) NOT NULL,
    [ReturnDate] date NOT NULL,
    [Status] int NOT NULL,
    [Reason] nvarchar(500) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [CreditNoteId] uniqueidentifier NULL,
    [CreditNoteNumber] nvarchar(50) NULL,
    [SubTotal] decimal(19,4) NOT NULL,
    [TaxTotal] decimal(19,4) NOT NULL,
    [TotalAmount] decimal(19,4) NOT NULL,
    [VoidReason] nvarchar(500) NULL,
    [VoidedAt] datetime2 NULL,
    [VoidedBy] uniqueidentifier NULL,
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
    CONSTRAINT [PK_sls_Returns] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [svc_RetainerPeriods] (
    [Id] uniqueidentifier NOT NULL,
    [RetainerId] uniqueidentifier NOT NULL,
    [PeriodStart] date NOT NULL,
    [PeriodEnd] date NOT NULL,
    [RecurringFee] decimal(18,2) NOT NULL,
    [IncludedHours] decimal(18,2) NOT NULL,
    [BillableHours] decimal(18,2) NOT NULL,
    [OverageHours] decimal(18,2) NOT NULL,
    [OverageHourlyRate] decimal(18,2) NOT NULL,
    [OverageAmount] decimal(18,2) NOT NULL,
    [InvoiceId] uniqueidentifier NOT NULL,
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
    CONSTRAINT [PK_svc_RetainerPeriods] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [ast_Assets] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(50) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [CategoryId] uniqueidentifier NOT NULL,
    [AcquisitionDate] date NOT NULL,
    [Cost] decimal(18,2) NOT NULL,
    [SalvageValue] decimal(18,2) NOT NULL,
    [Method] nvarchar(30) NOT NULL,
    [LifeMonths] int NOT NULL,
    [DecliningRatePercent] decimal(7,4) NULL,
    [BusinessUnitId] uniqueidentifier NULL,
    [Custodian] nvarchar(200) NULL,
    [Status] nvarchar(30) NOT NULL,
    [AccumulatedDepreciation] decimal(18,2) NOT NULL,
    [CapitalisedOn] date NULL,
    [CapitalisationSource] nvarchar(30) NULL,
    [CapitalisationJournalId] uniqueidentifier NULL,
    [SourceBillId] uniqueidentifier NULL,
    [SourceBillLineId] uniqueidentifier NULL,
    [DisposedOn] date NULL,
    [DisposalProceeds] decimal(18,2) NULL,
    [DisposalGainLoss] decimal(18,2) NULL,
    [DisposalJournalId] uniqueidentifier NULL,
    [DisposalReason] nvarchar(400) NULL,
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
    CONSTRAINT [PK_ast_Assets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ast_Assets_ast_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [ast_Categories] ([Id])
);
GO

CREATE TABLE [ind_RecordEvents] (
    [Id] uniqueidentifier NOT NULL,
    [RecordId] uniqueidentifier NOT NULL,
    [FromStatus] nvarchar(40) NULL,
    [ToStatus] nvarchar(40) NOT NULL,
    [OccurredAt] datetime2 NOT NULL,
    [UserId] uniqueidentifier NULL,
    [UserName] nvarchar(200) NULL,
    [Reason] nvarchar(1000) NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ind_RecordEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ind_RecordEvents_ind_Records_RecordId] FOREIGN KEY ([RecordId]) REFERENCES [ind_Records] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ind_RecordLines] (
    [Id] uniqueidentifier NOT NULL,
    [RecordId] uniqueidentifier NOT NULL,
    [Seq] int NOT NULL,
    [Code] nvarchar(50) NULL,
    [Description] nvarchar(500) NOT NULL,
    [ItemId] uniqueidentifier NULL,
    [UnitOfMeasure] nvarchar(30) NULL,
    [Quantity] decimal(19,4) NOT NULL,
    [UnitPrice] decimal(19,4) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [CompletedQuantity] decimal(19,4) NOT NULL,
    [DueDate] date NULL,
    [InvoiceId] uniqueidentifier NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_ind_RecordLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ind_RecordLines_ind_Records_RecordId] FOREIGN KEY ([RecordId]) REFERENCES [ind_Records] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [pur_ReturnBillShares] (
    [Id] uniqueidentifier NOT NULL,
    [ReturnId] uniqueidentifier NOT NULL,
    [BillId] uniqueidentifier NOT NULL,
    [OrderLineId] uniqueidentifier NULL,
    [OrderBillLineId] uniqueidentifier NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [AppliedToBill] decimal(19,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_pur_ReturnBillShares] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_pur_ReturnBillShares_pur_Returns_ReturnId] FOREIGN KEY ([ReturnId]) REFERENCES [pur_Returns] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [pur_ReturnLines] (
    [Id] uniqueidentifier NOT NULL,
    [ReturnId] uniqueidentifier NOT NULL,
    [OrderLineId] uniqueidentifier NULL,
    [BillLineId] uniqueidentifier NULL,
    [ItemId] uniqueidentifier NULL,
    [ItemCode] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_pur_ReturnLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_pur_ReturnLines_pur_Returns_ReturnId] FOREIGN KEY ([ReturnId]) REFERENCES [pur_Returns] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [sls_ReturnLines] (
    [Id] uniqueidentifier NOT NULL,
    [ReturnId] uniqueidentifier NOT NULL,
    [OrderLineId] uniqueidentifier NULL,
    [InvoiceLineId] uniqueidentifier NULL,
    [ItemId] uniqueidentifier NULL,
    [ItemCode] nvarchar(50) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Quantity] decimal(18,4) NOT NULL,
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
    CONSTRAINT [PK_sls_ReturnLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_sls_ReturnLines_sls_Returns_ReturnId] FOREIGN KEY ([ReturnId]) REFERENCES [sls_Returns] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [ast_DepreciationEntries] (
    [Id] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [AssetId] uniqueidentifier NOT NULL,
    [PeriodEnd] date NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [AccumulatedAfter] decimal(18,2) NOT NULL,
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
    CONSTRAINT [PK_ast_DepreciationEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ast_DepreciationEntries_ast_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [ast_Assets] ([Id]),
    CONSTRAINT [FK_ast_DepreciationEntries_ast_DepreciationRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [ast_DepreciationRuns] ([Id])
);
GO

CREATE INDEX [IX_svc_TimeEntries_InvoiceId] ON [svc_TimeEntries] ([InvoiceId]) WHERE [InvoiceId] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_ApprovalSteps_ApprovalRequestId_Level] ON [ApprovalSteps] ([ApprovalRequestId], [Level]);
GO

CREATE INDEX [IX_ast_Assets_CategoryId] ON [ast_Assets] ([CategoryId]);
GO

CREATE UNIQUE INDEX [IX_ast_Assets_OrgId_LegalEntityId_Number] ON [ast_Assets] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_ast_Assets_OrgId_LegalEntityId_Status] ON [ast_Assets] ([OrgId], [LegalEntityId], [Status]);
GO

CREATE INDEX [IX_ast_Assets_SourceBillLineId] ON [ast_Assets] ([SourceBillLineId]);
GO

CREATE INDEX [IX_ast_Categories_AccumulatedDepreciationAccountId] ON [ast_Categories] ([AccumulatedDepreciationAccountId]);
GO

CREATE INDEX [IX_ast_Categories_AssetAccountId] ON [ast_Categories] ([AssetAccountId]);
GO

CREATE INDEX [IX_ast_Categories_DepreciationExpenseAccountId] ON [ast_Categories] ([DepreciationExpenseAccountId]);
GO

CREATE INDEX [IX_ast_Categories_DisposalGainLossAccountId] ON [ast_Categories] ([DisposalGainLossAccountId]);
GO

CREATE UNIQUE INDEX [IX_ast_Categories_OrgId_Code] ON [ast_Categories] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_ast_DepreciationEntries_AssetId_PeriodEnd] ON [ast_DepreciationEntries] ([AssetId], [PeriodEnd]);
GO

CREATE INDEX [IX_ast_DepreciationEntries_RunId] ON [ast_DepreciationEntries] ([RunId]);
GO

CREATE INDEX [IX_ast_DepreciationRuns_JournalEntryId] ON [ast_DepreciationRuns] ([JournalEntryId]);
GO

CREATE UNIQUE INDEX [IX_ast_DepreciationRuns_OrgId_LegalEntityId_Year_Month] ON [ast_DepreciationRuns] ([OrgId], [LegalEntityId], [Year], [Month]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_FiscalYearCloses_JournalEntryId] ON [FiscalYearCloses] ([JournalEntryId]);
GO

CREATE INDEX [IX_FiscalYearCloses_LegalEntityId] ON [FiscalYearCloses] ([LegalEntityId]);
GO

CREATE UNIQUE INDEX [IX_FiscalYearCloses_OrgId_LegalEntityId_FiscalYear] ON [FiscalYearCloses] ([OrgId], [LegalEntityId], [FiscalYear]) WHERE [ReopenedAt] IS NULL AND [IsDeleted] = 0;
GO

CREATE INDEX [IX_ind_RecordEvents_RecordId] ON [ind_RecordEvents] ([RecordId]);
GO

CREATE INDEX [IX_ind_RecordLines_RecordId] ON [ind_RecordLines] ([RecordId]);
GO

CREATE UNIQUE INDEX [IX_ind_Records_OrgId_ModuleKey_RecordType_Number] ON [ind_Records] ([OrgId], [ModuleKey], [RecordType], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_ind_Records_OrgId_ModuleKey_RecordType_Status] ON [ind_Records] ([OrgId], [ModuleKey], [RecordType], [Status]);
GO

CREATE INDEX [IX_ind_Records_OrgId_ParentId] ON [ind_Records] ([OrgId], [ParentId]);
GO

CREATE INDEX [IX_mfg_WorkOrderMaterialIssues_OrgId_WorkOrderId] ON [mfg_WorkOrderMaterialIssues] ([OrgId], [WorkOrderId]);
GO

CREATE INDEX [IX_mfg_WorkOrderMaterialIssues_WorkOrderId] ON [mfg_WorkOrderMaterialIssues] ([WorkOrderId]);
GO

CREATE INDEX [IX_pur_OrderBillLines_OrgId_BillId] ON [pur_OrderBillLines] ([OrgId], [BillId]);
GO

CREATE INDEX [IX_pur_OrderBillLines_OrgId_OrderId] ON [pur_OrderBillLines] ([OrgId], [OrderId]);
GO

CREATE INDEX [IX_pur_ReturnBillShares_OrgId_ReturnId] ON [pur_ReturnBillShares] ([OrgId], [ReturnId]);
GO

CREATE INDEX [IX_pur_ReturnBillShares_ReturnId] ON [pur_ReturnBillShares] ([ReturnId]);
GO

CREATE INDEX [IX_pur_ReturnLines_OrgId_ReturnId] ON [pur_ReturnLines] ([OrgId], [ReturnId]);
GO

CREATE INDEX [IX_pur_ReturnLines_ReturnId] ON [pur_ReturnLines] ([ReturnId]);
GO

CREATE INDEX [IX_pur_Returns_OrgId_BillId] ON [pur_Returns] ([OrgId], [BillId]);
GO

CREATE INDEX [IX_pur_Returns_OrgId_LegalEntityId_Status] ON [pur_Returns] ([OrgId], [LegalEntityId], [Status]);
GO

CREATE UNIQUE INDEX [IX_pur_Returns_OrgId_Number] ON [pur_Returns] ([OrgId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_pur_Returns_OrgId_PurchaseOrderId] ON [pur_Returns] ([OrgId], [PurchaseOrderId]);
GO

CREATE INDEX [IX_re_OwnerPayouts_OrgId_BillId] ON [re_OwnerPayouts] ([OrgId], [BillId]) WHERE [BillId] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_re_OwnerPayouts_OrgId_LegalEntityId_Number] ON [re_OwnerPayouts] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_re_OwnerPayouts_OrgId_OwnerPartyId_PeriodFrom] ON [re_OwnerPayouts] ([OrgId], [OwnerPartyId], [PeriodFrom]);
GO

CREATE INDEX [IX_sls_ReturnLines_OrgId_ReturnId] ON [sls_ReturnLines] ([OrgId], [ReturnId]);
GO

CREATE INDEX [IX_sls_ReturnLines_ReturnId] ON [sls_ReturnLines] ([ReturnId]);
GO

CREATE INDEX [IX_sls_Returns_OrgId_InvoiceId] ON [sls_Returns] ([OrgId], [InvoiceId]);
GO

CREATE INDEX [IX_sls_Returns_OrgId_LegalEntityId_Status] ON [sls_Returns] ([OrgId], [LegalEntityId], [Status]);
GO

CREATE UNIQUE INDEX [IX_sls_Returns_OrgId_Number] ON [sls_Returns] ([OrgId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_sls_Returns_OrgId_SalesOrderId] ON [sls_Returns] ([OrgId], [SalesOrderId]);
GO

CREATE INDEX [IX_svc_RetainerPeriods_InvoiceId] ON [svc_RetainerPeriods] ([InvoiceId]);
GO

CREATE UNIQUE INDEX [IX_svc_RetainerPeriods_OrgId_RetainerId_PeriodStart] ON [svc_RetainerPeriods] ([OrgId], [RetainerId], [PeriodStart]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106192957_MultiIndustryErp', N'8.0.11');
GO

COMMIT;
GO

