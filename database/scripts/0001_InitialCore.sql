IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Accounts] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [ReportingGroup] nvarchar(400) NULL,
        [ParentId] uniqueidentifier NULL,
        [IsPostable] bit NOT NULL,
        [IsSystem] bit NOT NULL,
        [IsBank] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [SystemRole] nvarchar(400) NULL,
        [CurrencyCode] nvarchar(400) NULL,
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
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Accounts_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [AppRoles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Description] nvarchar(400) NULL,
        [IsBuiltIn] bit NOT NULL,
        [ModuleKey] nvarchar(400) NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_AppRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [ApprovalPolicies] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(400) NOT NULL,
        [MinAmount] decimal(18,2) NULL,
        [ApproverPermission] nvarchar(400) NOT NULL,
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
        CONSTRAINT [PK_ApprovalPolicies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [ApprovalRequests] (
        [Id] uniqueidentifier NOT NULL,
        [LegalEntityId] uniqueidentifier NULL,
        [DocumentType] nvarchar(400) NOT NULL,
        [DocumentId] uniqueidentifier NULL,
        [Subject] nvarchar(400) NOT NULL,
        [Justification] nvarchar(400) NULL,
        [Amount] decimal(18,2) NULL,
        [CurrencyCode] nvarchar(400) NULL,
        [RequestedBy] uniqueidentifier NOT NULL,
        [RequestedAt] datetime2 NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [DecidedBy] uniqueidentifier NULL,
        [DecidedAt] datetime2 NULL,
        [DecisionComment] nvarchar(400) NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ApprovalRequests] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Attachments] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerType] nvarchar(400) NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(400) NOT NULL,
        [ContentType] nvarchar(400) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [StorageKey] nvarchar(400) NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(400) NOT NULL,
        [RecordType] nvarchar(400) NOT NULL,
        [RecordId] nvarchar(400) NOT NULL,
        [ChangedFields] nvarchar(4000) NULL,
        [Before] nvarchar(max) NULL,
        [After] nvarchar(max) NULL,
        [IpAddress] nvarchar(400) NULL,
        [CorrelationId] nvarchar(400) NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Bills] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(400) NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [VendorReference] nvarchar(400) NULL,
        [BillDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [CurrencyCode] nvarchar(400) NOT NULL,
        [ExchangeRate] decimal(18,8) NOT NULL,
        [Net] decimal(18,2) NOT NULL,
        [Tax] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Paid] decimal(18,2) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ApprovalRequestId] uniqueidentifier NULL,
        [JournalEntryId] uniqueidentifier NULL,
        [SourceType] nvarchar(400) NULL,
        [SourceId] uniqueidentifier NULL,
        [Notes] nvarchar(400) NULL,
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
        CONSTRAINT [PK_Bills] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Bills_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [BusinessUnits] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(400) NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Address] nvarchar(400) NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_BusinessUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_BusinessUnits_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Currencies] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nchar(3) NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Symbol] nvarchar(400) NULL,
        [Decimals] int NOT NULL,
        CONSTRAINT [PK_Currencies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [CustomFieldDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [Key] nvarchar(64) NOT NULL,
        [Label] nvarchar(100) NOT NULL,
        [DataType] nvarchar(40) NOT NULL,
        [Required] bit NOT NULL,
        [Options] nvarchar(max) NULL,
        [DefaultValue] nvarchar(400) NULL,
        [LegalEntityId] uniqueidentifier NULL,
        [Order] int NOT NULL,
        [Section] nvarchar(400) NULL,
        [HelpText] nvarchar(400) NULL,
        [IsSearchable] bit NOT NULL,
        [ShowInList] bit NOT NULL,
        [Sortable] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [ModuleKey] nvarchar(400) NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CustomFieldDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CustomFieldDefinitions_Options_IsJson] CHECK ([Options] IS NULL OR ISJSON([Options]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [ExchangeRates] (
        [Id] uniqueidentifier NOT NULL,
        [FromCurrency] nvarchar(400) NOT NULL,
        [ToCurrency] nvarchar(400) NOT NULL,
        [RateDate] date NOT NULL,
        [Rate] decimal(18,8) NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ExchangeRates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [inv_Warehouses] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(400) NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_inv_Warehouses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_inv_Warehouses_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(400) NOT NULL,
        [IsCreditNote] bit NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [InvoiceDate] date NOT NULL,
        [DueDate] date NOT NULL,
        [CurrencyCode] nvarchar(400) NOT NULL,
        [ExchangeRate] decimal(18,8) NOT NULL,
        [Net] decimal(18,2) NOT NULL,
        [Tax] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [Paid] decimal(18,2) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [SourceType] nvarchar(400) NULL,
        [SourceId] uniqueidentifier NULL,
        [BusinessUnitId] uniqueidentifier NULL,
        [JournalEntryId] uniqueidentifier NULL,
        [Notes] nvarchar(400) NULL,
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
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Invoices_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Items] (
        [Id] uniqueidentifier NOT NULL,
        [Sku] nvarchar(400) NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Type] nvarchar(40) NOT NULL,
        [Uom] nvarchar(400) NOT NULL,
        [SalesPrice] decimal(18,2) NULL,
        [PurchasePrice] decimal(18,2) NULL,
        [TaxCodeId] uniqueidentifier NULL,
        [IncomeAccountId] uniqueidentifier NULL,
        [ExpenseAccountId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_Items] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Items_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(400) NOT NULL,
        [Date] date NOT NULL,
        [Memo] nvarchar(400) NULL,
        [Reference] nvarchar(400) NULL,
        [SourceType] nvarchar(400) NOT NULL,
        [SourceId] uniqueidentifier NULL,
        [CurrencyCode] nvarchar(400) NOT NULL,
        [ExchangeRate] decimal(18,8) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [PostedAt] datetime2 NULL,
        [PostedBy] uniqueidentifier NULL,
        [ReversalOfId] uniqueidentifier NULL,
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
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [LegalEntities] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [LegalName] nvarchar(400) NULL,
        [CountryCode] nchar(2) NOT NULL,
        [BaseCurrency] nchar(3) NOT NULL,
        [TaxRegistrationNo] nvarchar(400) NULL,
        [IsTaxRegistered] bit NOT NULL,
        [RegistrationNo] nvarchar(400) NULL,
        [Address] nvarchar(400) NULL,
        [Email] nvarchar(400) NULL,
        [Phone] nvarchar(400) NULL,
        [BooksLockedTo] date NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_LegalEntities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LegalEntities_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(400) NOT NULL,
        [Body] nvarchar(400) NULL,
        [Link] nvarchar(400) NULL,
        [Severity] nvarchar(400) NOT NULL,
        [ReadAt] datetime2 NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [NumberSequences] (
        [Id] uniqueidentifier NOT NULL,
        [LegalEntityId] uniqueidentifier NULL,
        [DocumentType] nvarchar(400) NOT NULL,
        [Prefix] nvarchar(400) NOT NULL,
        [Pattern] nvarchar(400) NOT NULL,
        [ResetYearly] bit NOT NULL,
        [Year] int NOT NULL,
        [NextValue] bigint NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_NumberSequences] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [OrganizationMembers] (
        [Id] uniqueidentifier NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LastSignInAt] datetime2 NULL,
        CONSTRAINT [PK_OrganizationMembers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [OrganizationModules] (
        [Id] uniqueidentifier NOT NULL,
        [ModuleKey] nvarchar(400) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [EnabledAt] datetime2 NULL,
        [DisabledAt] datetime2 NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_OrganizationModules] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Organizations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [IndustryCode] nvarchar(400) NOT NULL,
        [CountryCode] nchar(2) NOT NULL,
        [BaseCurrency] nchar(3) NOT NULL,
        [TimeZone] nvarchar(400) NOT NULL,
        [DefaultLocale] nvarchar(400) NOT NULL,
        [Plan] nvarchar(400) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Parties] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(400) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Roles] int NOT NULL,
        [Email] nvarchar(400) NULL,
        [Phone] nvarchar(400) NULL,
        [TaxNumber] nvarchar(400) NULL,
        [IdNumber] nvarchar(400) NULL,
        [Iban] nvarchar(400) NULL,
        [CurrencyCode] nvarchar(400) NULL,
        [PaymentTermsDays] int NULL,
        [Address] nvarchar(400) NULL,
        [IsActive] bit NOT NULL,
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
        CONSTRAINT [PK_Parties] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Parties_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [UsedAt] datetime2 NULL,
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [PaymentInstruments] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(400) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Direction] nvarchar(40) NOT NULL,
        [InstrumentNo] nvarchar(400) NOT NULL,
        [BankName] nvarchar(400) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(400) NOT NULL,
        [InstrumentDate] date NOT NULL,
        [ReceivedOn] date NULL,
        [PartyId] uniqueidentifier NULL,
        [Status] nvarchar(40) NOT NULL,
        [SourceType] nvarchar(400) NULL,
        [SourceId] uniqueidentifier NULL,
        [JournalEntryId] uniqueidentifier NULL,
        [Notes] nvarchar(400) NULL,
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
        CONSTRAINT [PK_PaymentInstruments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [TokenHash] nvarchar(100) NOT NULL,
        [FamilyId] uniqueidentifier NOT NULL,
        [IsPersistent] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [RevokedReason] nvarchar(400) NULL,
        [ReplacedById] uniqueidentifier NULL,
        [CreatedByIp] nvarchar(400) NULL,
        [UserAgent] nvarchar(400) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Settings] (
        [Id] uniqueidentifier NOT NULL,
        [LegalEntityId] uniqueidentifier NULL,
        [Key] nvarchar(400) NOT NULL,
        [Value] nvarchar(4000) NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Settlements] (
        [Id] uniqueidentifier NOT NULL,
        [Number] nvarchar(400) NOT NULL,
        [Direction] nvarchar(40) NOT NULL,
        [PartyId] uniqueidentifier NOT NULL,
        [Date] date NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [BankAccountId] uniqueidentifier NULL,
        [PaymentInstrumentId] uniqueidentifier NULL,
        [CurrencyCode] nvarchar(400) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Unallocated] decimal(18,2) NOT NULL,
        [Reference] nvarchar(400) NULL,
        [Status] nvarchar(40) NOT NULL,
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
        CONSTRAINT [PK_Settlements] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [TaxCodes] (
        [Id] uniqueidentifier NOT NULL,
        [LegalEntityId] uniqueidentifier NULL,
        [Code] nvarchar(400) NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Kind] nvarchar(40) NOT NULL,
        [Rate] decimal(9,4) NOT NULL,
        [IsRecoverable] bit NOT NULL,
        [IsDefault] bit NOT NULL,
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
        CONSTRAINT [PK_TaxCodes] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [UserLegalEntityAccess] (
        [Id] uniqueidentifier NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LegalEntityId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLegalEntityAccess] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastSignInAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(400) NULL,
        [SecurityStamp] nvarchar(400) NULL,
        [ConcurrencyStamp] nvarchar(400) NULL,
        [PhoneNumber] nvarchar(400) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [AppUserRoles] (
        [Id] uniqueidentifier NOT NULL,
        [OrgId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AppUserRoles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AppUserRoles_AppRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [Permission] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolePermissions_AppRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AppRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [BillLine] (
        [Id] uniqueidentifier NOT NULL,
        [BillId] uniqueidentifier NOT NULL,
        [Seq] int NOT NULL,
        [ItemId] uniqueidentifier NULL,
        [Description] nvarchar(400) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [TaxCodeId] uniqueidentifier NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TaxRecoverable] bit NOT NULL,
        [Net] decimal(18,2) NOT NULL,
        [AccountId] uniqueidentifier NULL,
        CONSTRAINT [PK_BillLine] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BillLine_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [InvoiceLine] (
        [Id] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NOT NULL,
        [Seq] int NOT NULL,
        [ItemId] uniqueidentifier NULL,
        [Description] nvarchar(400) NOT NULL,
        [Quantity] decimal(18,4) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [TaxCodeId] uniqueidentifier NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [Net] decimal(18,2) NOT NULL,
        [AccountId] uniqueidentifier NULL,
        CONSTRAINT [PK_InvoiceLine] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoiceLine_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [JournalLines] (
        [Id] uniqueidentifier NOT NULL,
        [JournalEntryId] uniqueidentifier NOT NULL,
        [Seq] int NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Debit] decimal(18,2) NOT NULL,
        [Credit] decimal(18,2) NOT NULL,
        [Description] nvarchar(400) NULL,
        [PartyId] uniqueidentifier NULL,
        [BusinessUnitId] uniqueidentifier NULL,
        [Dimensions] nvarchar(400) NULL,
        CONSTRAINT [PK_JournalLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [SettlementAllocation] (
        [Id] uniqueidentifier NOT NULL,
        [SettlementId] uniqueidentifier NOT NULL,
        [InvoiceId] uniqueidentifier NULL,
        [BillId] uniqueidentifier NULL,
        [Amount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_SettlementAllocation] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SettlementAllocation_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(400) NULL,
        [ClaimValue] nvarchar(400) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [UserLogins] (
        [LoginProvider] nvarchar(400) NOT NULL,
        [ProviderKey] nvarchar(400) NOT NULL,
        [ProviderDisplayName] nvarchar(400) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE TABLE [UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(400) NOT NULL,
        [Name] nvarchar(400) NOT NULL,
        [Value] nvarchar(400) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Accounts_OrgId_Code] ON [Accounts] ([OrgId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppRoles_OrgId_Name] ON [AppRoles] ([OrgId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_ApprovalRequests_OrgId_Status] ON [ApprovalRequests] ([OrgId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppUserRoles_OrgId_UserId_RoleId] ON [AppUserRoles] ([OrgId], [UserId], [RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_AppUserRoles_RoleId] ON [AppUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_Attachments_OrgId_OwnerType_OwnerId] ON [Attachments] ([OrgId], [OwnerType], [OwnerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_OrgId_OccurredAt] ON [AuditEvents] ([OrgId], [OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_OrgId_RecordType_RecordId] ON [AuditEvents] ([OrgId], [RecordType], [RecordId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_BillLine_BillId] ON [BillLine] ([BillId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bills_OrgId_LegalEntityId_Number] ON [Bills] ([OrgId], [LegalEntityId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_Bills_OrgId_PartyId_VendorReference] ON [Bills] ([OrgId], [PartyId], [VendorReference]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessUnits_OrgId_LegalEntityId_Code] ON [BusinessUnits] ([OrgId], [LegalEntityId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Currencies_Code] ON [Currencies] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomFieldDefinitions_OrgId_EntityType_Key] ON [CustomFieldDefinitions] ([OrgId], [EntityType], [Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExchangeRates_OrgId_FromCurrency_ToCurrency_RateDate] ON [ExchangeRates] ([OrgId], [FromCurrency], [ToCurrency], [RateDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_inv_Warehouses_OrgId_Code] ON [inv_Warehouses] ([OrgId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_InvoiceLine_InvoiceId] ON [InvoiceLine] ([InvoiceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_OrgId_LegalEntityId_Number] ON [Invoices] ([OrgId], [LegalEntityId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Items_OrgId_Sku] ON [Items] ([OrgId], [Sku]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_JournalEntries_OrgId_LegalEntityId_Number] ON [JournalEntries] ([OrgId], [LegalEntityId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_JournalLines_AccountId_JournalEntryId] ON [JournalLines] ([AccountId], [JournalEntryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_JournalLines_JournalEntryId] ON [JournalLines] ([JournalEntryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LegalEntities_OrgId_Code] ON [LegalEntities] ([OrgId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_Notifications_OrgId_UserId_ReadAt] ON [Notifications] ([OrgId], [UserId], [ReadAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_NumberSequences_OrgId_LegalEntityId_DocumentType_Year] ON [NumberSequences] ([OrgId], [LegalEntityId], [DocumentType], [Year]) WHERE [LegalEntityId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationMembers_OrgId_UserId] ON [OrganizationMembers] ([OrgId], [UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationModules_OrgId_ModuleKey] ON [OrganizationModules] ([OrgId], [ModuleKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Organizations_Code] ON [Organizations] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Parties_OrgId_Code] ON [Parties] ([OrgId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_Parties_OrgId_Name] ON [Parties] ([OrgId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PasswordResetTokens_TokenHash] ON [PasswordResetTokens] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentInstruments_OrgId_LegalEntityId_Number] ON [PaymentInstruments] ([OrgId], [LegalEntityId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_FamilyId] ON [RefreshTokens] ([FamilyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_Permission] ON [RolePermissions] ([RoleId], [Permission]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Settings_OrgId_LegalEntityId_Key] ON [Settings] ([OrgId], [LegalEntityId], [Key]) WHERE [LegalEntityId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_SettlementAllocation_SettlementId] ON [SettlementAllocation] ([SettlementId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settlements_OrgId_LegalEntityId_Number] ON [Settlements] ([OrgId], [LegalEntityId], [Number]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TaxCodes_OrgId_LegalEntityId_Code] ON [TaxCodes] ([OrgId], [LegalEntityId], [Code]) WHERE [LegalEntityId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserLegalEntityAccess_OrgId_UserId_LegalEntityId] ON [UserLegalEntityAccess] ([OrgId], [UserId], [LegalEntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261022132212_InitialCore'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261022132212_InitialCore', N'8.0.11');
END;
GO

COMMIT;
GO

