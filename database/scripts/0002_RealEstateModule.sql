BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_Properties] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [PropertyType] int NOT NULL,
        [OwnershipType] int NOT NULL,
        [OwnerPartyId] uniqueidentifier NULL,
        [Region] nvarchar(100) NULL,
        [Community] nvarchar(100) NULL,
        [PlotNumber] nvarchar(50) NULL,
        [YearBuilt] int NULL,
        [TotalUnits] int NOT NULL,
        [IndicativeValue] decimal(19,4) NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(2000) NULL,
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
        CONSTRAINT [PK_re_Properties] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_re_Properties_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_UnitTypes] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [DefaultUsage] int NOT NULL,
        [DefaultBedrooms] int NULL,
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
        CONSTRAINT [PK_re_UnitTypes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_Units] (
        [Id] uniqueidentifier NOT NULL,
        [PropertyId] uniqueidentifier NOT NULL,
        [UnitNumber] nvarchar(30) NOT NULL,
        [UnitTypeId] uniqueidentifier NOT NULL,
        [Usage] int NOT NULL,
        [Floor] nvarchar(10) NULL,
        [Bedrooms] int NULL,
        [AreaSqft] decimal(10,2) NOT NULL,
        [AskingRent] decimal(19,4) NOT NULL,
        [Status] int NOT NULL,
        [VacantSince] date NULL,
        [ReservedUntil] date NULL,
        [UnavailableReason] nvarchar(200) NULL,
        [Notes] nvarchar(2000) NULL,
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
        CONSTRAINT [PK_re_Units] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_re_Units_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1),
        CONSTRAINT [FK_re_Units_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_re_Units_re_UnitTypes_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [re_UnitTypes] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_BrokerageDeals] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(30) NULL,
        [DealType] int NOT NULL,
        [PropertyId] uniqueidentifier NULL,
        [UnitId] uniqueidentifier NULL,
        [UnitReference] nvarchar(100) NULL,
        [ClientPartyId] uniqueidentifier NULL,
        [ClientName] nvarchar(200) NOT NULL,
        [AgentPartyId] uniqueidentifier NULL,
        [ExternalAgentName] nvarchar(200) NULL,
        [TeamLeadPartyId] uniqueidentifier NULL,
        [DealDate] date NOT NULL,
        [Stage] int NOT NULL,
        [DealValue] decimal(19,4) NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [CommissionRatePercent] decimal(9,4) NOT NULL,
        [TaxCodeId] uniqueidentifier NULL,
        [AgentSharePercent] decimal(9,4) NOT NULL,
        [TeamLeadSharePercent] decimal(9,4) NOT NULL,
        [GrossCommission] decimal(19,4) NOT NULL,
        [LostReason] nvarchar(500) NULL,
        [Notes] nvarchar(2000) NULL,
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
        CONSTRAINT [PK_re_BrokerageDeals] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_re_BrokerageDeals_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1),
        CONSTRAINT [FK_re_BrokerageDeals_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_re_BrokerageDeals_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_Leases] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(30) NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [PropertyId] uniqueidentifier NOT NULL,
        [LesseePartyId] uniqueidentifier NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [AnnualRent] decimal(19,4) NOT NULL,
        [CurrencyCode] nchar(3) NOT NULL,
        [PaymentFrequency] int NOT NULL,
        [SecurityDepositAmount] decimal(19,4) NOT NULL,
        [TaxCodeId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [ActivatedAt] datetime2 NULL,
        [ActivatedBy] uniqueidentifier NULL,
        [TerminationDate] date NULL,
        [TerminationReason] nvarchar(500) NULL,
        [RenewalOfLeaseId] uniqueidentifier NULL,
        [GraceDays] int NOT NULL,
        [NoticePeriodDays] int NOT NULL,
        [HoldingOverFlag] bit NOT NULL,
        [Notes] nvarchar(2000) NULL,
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
        CONSTRAINT [PK_re_Leases] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_re_Leases_Amounts] CHECK ([AnnualRent] > 0 AND [SecurityDepositAmount] >= 0),
        CONSTRAINT [CK_re_Leases_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1),
        CONSTRAINT [CK_re_Leases_Dates] CHECK ([EndDate] > [StartDate]),
        CONSTRAINT [CK_re_Leases_PaymentFrequency] CHECK ([PaymentFrequency] IN (1, 2, 4, 6, 12)),
        CONSTRAINT [FK_re_Leases_re_Leases_RenewalOfLeaseId] FOREIGN KEY ([RenewalOfLeaseId]) REFERENCES [re_Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_re_Leases_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_re_Leases_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_UnitRentRates] (
        [Id] uniqueidentifier NOT NULL,
        [UnitId] uniqueidentifier NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [AnnualRent] decimal(19,4) NOT NULL,
        [Reason] nvarchar(200) NULL,
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
        CONSTRAINT [PK_re_UnitRentRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_re_UnitRentRates_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_RentInstalments] (
        [Id] uniqueidentifier NOT NULL,
        [LeaseId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [PeriodFrom] date NOT NULL,
        [PeriodTo] date NOT NULL,
        [DueDate] date NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Status] int NOT NULL,
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
        [LegalEntityId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_re_RentInstalments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_re_RentInstalments_Period] CHECK ([PeriodTo] >= [PeriodFrom] AND [Amount] >= 0),
        CONSTRAINT [FK_re_RentInstalments_re_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [re_Leases] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE TABLE [re_WorkOrders] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(30) NULL,
        [PropertyId] uniqueidentifier NOT NULL,
        [UnitId] uniqueidentifier NULL,
        [LeaseId] uniqueidentifier NULL,
        [Issue] nvarchar(300) NOT NULL,
        [Category] nvarchar(50) NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [ReportedOn] date NOT NULL,
        [ReportedBy] nvarchar(100) NULL,
        [VendorPartyId] uniqueidentifier NULL,
        [Technician] nvarchar(100) NULL,
        [ScheduledFor] date NULL,
        [CompletedOn] date NULL,
        [EstimatedCost] decimal(19,4) NOT NULL,
        [ActualCost] decimal(19,4) NULL,
        [Details] nvarchar(4000) NULL,
        [ResolutionNotes] nvarchar(4000) NULL,
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
        CONSTRAINT [PK_re_WorkOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_re_WorkOrders_Costs] CHECK ([EstimatedCost] >= 0 AND ([ActualCost] IS NULL OR [ActualCost] >= 0)),
        CONSTRAINT [CK_re_WorkOrders_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1),
        CONSTRAINT [FK_re_WorkOrders_re_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [re_Leases] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_re_WorkOrders_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_re_WorkOrders_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_BrokerageDeals_OrgId_AgentPartyId] ON [re_BrokerageDeals] ([OrgId], [AgentPartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_BrokerageDeals_OrgId_LegalEntityId_Number] ON [re_BrokerageDeals] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0 AND [Number] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_BrokerageDeals_OrgId_LegalEntityId_Stage_DealDate] ON [re_BrokerageDeals] ([OrgId], [LegalEntityId], [Stage], [DealDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_BrokerageDeals_PropertyId] ON [re_BrokerageDeals] ([PropertyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_BrokerageDeals_UnitId] ON [re_BrokerageDeals] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_Leases_OrgId_LegalEntityId_Number] ON [re_Leases] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0 AND [Number] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Leases_OrgId_LegalEntityId_Status_EndDate] ON [re_Leases] ([OrgId], [LegalEntityId], [Status], [EndDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Leases_OrgId_LesseePartyId] ON [re_Leases] ([OrgId], [LesseePartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Leases_OrgId_PropertyId] ON [re_Leases] ([OrgId], [PropertyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Leases_PropertyId] ON [re_Leases] ([PropertyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Leases_RenewalOfLeaseId] ON [re_Leases] ([RenewalOfLeaseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Leases_UnitId] ON [re_Leases] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_re_Leases_OneOccupyingPerUnit] ON [re_Leases] ([OrgId], [UnitId]) WHERE [IsDeleted] = 0 AND [Status] IN (1, 5)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_re_Leases_OneRenewal] ON [re_Leases] ([OrgId], [RenewalOfLeaseId]) WHERE [IsDeleted] = 0 AND [RenewalOfLeaseId] IS NOT NULL AND [Status] <> 6');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_Properties_OrgId_Code] ON [re_Properties] ([OrgId], [Code]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Properties_OrgId_LegalEntityId_IsActive] ON [re_Properties] ([OrgId], [LegalEntityId], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Properties_OrgId_OwnerPartyId] ON [re_Properties] ([OrgId], [OwnerPartyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_RentInstalments_LeaseId] ON [re_RentInstalments] ([LeaseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_re_RentInstalments_OrgId_DueDate] ON [re_RentInstalments] ([OrgId], [DueDate]) WHERE [IsDeleted] = 0 AND [Status] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_RentInstalments_OrgId_LeaseId_Sequence] ON [re_RentInstalments] ([OrgId], [LeaseId], [Sequence]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_UnitRentRates_OrgId_UnitId_EffectiveFrom] ON [re_UnitRentRates] ([OrgId], [UnitId], [EffectiveFrom]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_UnitRentRates_UnitId] ON [re_UnitRentRates] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Units_OrgId_LegalEntityId_Status_PropertyId] ON [re_Units] ([OrgId], [LegalEntityId], [Status], [PropertyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_Units_OrgId_PropertyId_UnitNumber] ON [re_Units] ([OrgId], [PropertyId], [UnitNumber]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Units_PropertyId] ON [re_Units] ([PropertyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_Units_UnitTypeId] ON [re_Units] ([UnitTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_UnitTypes_OrgId_Code] ON [re_UnitTypes] ([OrgId], [Code]) WHERE [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_WorkOrders_LeaseId] ON [re_WorkOrders] ([LeaseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_re_WorkOrders_OrgId_LegalEntityId_Number] ON [re_WorkOrders] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0 AND [Number] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_WorkOrders_OrgId_LegalEntityId_Status_Priority] ON [re_WorkOrders] ([OrgId], [LegalEntityId], [Status], [Priority]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_WorkOrders_OrgId_PropertyId_ReportedOn] ON [re_WorkOrders] ([OrgId], [PropertyId], [ReportedOn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_WorkOrders_OrgId_UnitId] ON [re_WorkOrders] ([OrgId], [UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_WorkOrders_PropertyId] ON [re_WorkOrders] ([PropertyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    CREATE INDEX [IX_re_WorkOrders_UnitId] ON [re_WorkOrders] ([UnitId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022145011_RealEstateModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022145011_RealEstateModule', N'8.0.11');
END;
GO

COMMIT;
GO

