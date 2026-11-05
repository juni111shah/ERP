BEGIN TRANSACTION;
GO

ALTER TABLE [Users] ADD [IsPlatformAdmin] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Organizations] ADD [IsInternal] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

CREATE TABLE [PlatformActivities] (
    [Id] uniqueidentifier NOT NULL,
    [At] datetime2 NOT NULL,
    [ActorUserId] uniqueidentifier NULL,
    [Actor] nvarchar(400) NOT NULL,
    [Action] nvarchar(400) NOT NULL,
    [OrgId] uniqueidentifier NULL,
    [ClientName] nvarchar(400) NULL,
    [Detail] nvarchar(1000) NULL,
    CONSTRAINT [PK_PlatformActivities] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [PlatformSettings] (
    [Id] uniqueidentifier NOT NULL,
    [SignupMode] nvarchar(40) NOT NULL,
    [RequireEmailVerification] bit NOT NULL,
    [DefaultPlanId] uniqueidentifier NULL,
    [Currency] nchar(3) NOT NULL,
    [TaxPercent] decimal(5,2) NOT NULL,
    [TaxLabel] nvarchar(400) NOT NULL,
    [InvoicePrefix] nvarchar(10) NOT NULL,
    [NextInvoiceNumber] int NOT NULL,
    [InvoiceDueDays] int NOT NULL,
    [RenewalLeadDays] int NOT NULL,
    [GraceDays] int NOT NULL,
    [SuspendAfterDays] int NOT NULL,
    [CompanyName] nvarchar(400) NOT NULL,
    [CompanyAddress] nvarchar(400) NULL,
    [CompanyTaxNumber] nvarchar(400) NULL,
    [SupportEmail] nvarchar(400) NULL,
    [TermsUrl] nvarchar(400) NULL,
    [PaymentInstructions] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_PlatformSettings] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SubscriptionPlans] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(40) NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [Description] nvarchar(500) NULL,
    [IsPublic] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [IsRecommended] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [MonthlyPrice] decimal(18,2) NOT NULL,
    [AnnualPrice] decimal(18,2) NOT NULL,
    [TrialDays] int NOT NULL,
    [MaxUsers] int NULL,
    [MaxLegalEntities] int NULL,
    [ModuleKeys] nvarchar(1000) NULL,
    [Features] nvarchar(2000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SubscriptionPlans] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SignupRequests] (
    [Id] uniqueidentifier NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [BusinessName] nvarchar(200) NOT NULL,
    [SignInKey] nvarchar(50) NOT NULL,
    [Industry] nvarchar(400) NOT NULL,
    [CountryCode] nchar(2) NOT NULL,
    [BaseCurrency] nchar(3) NOT NULL,
    [TimeZone] nvarchar(400) NULL,
    [ContactName] nvarchar(200) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [Phone] nvarchar(400) NULL,
    [PasswordHash] nvarchar(500) NULL,
    [PlanId] uniqueidentifier NOT NULL,
    [Cycle] nvarchar(40) NOT NULL,
    [VerificationTokenHash] nvarchar(64) NULL,
    [VerificationExpiresAt] datetime2 NULL,
    [EmailVerifiedAt] datetime2 NULL,
    [ReviewedAt] datetime2 NULL,
    [ReviewedBy] uniqueidentifier NULL,
    [RejectReason] nvarchar(400) NULL,
    [ProvisionedOrgId] uniqueidentifier NULL,
    [CreatedIp] nvarchar(400) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SignupRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SignupRequests_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WorkspaceSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [PlanId] uniqueidentifier NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [Cycle] nvarchar(40) NOT NULL,
    [StartedOn] date NOT NULL,
    [TrialEndsOn] date NULL,
    [CurrentPeriodStart] date NULL,
    [CurrentPeriodEnd] date NULL,
    [CustomPrice] decimal(18,2) NULL,
    [IsComplimentary] bit NOT NULL,
    [LapsedOn] date NULL,
    [SuspendedAt] datetime2 NULL,
    [SuspendedReason] nvarchar(400) NULL,
    [SuspendedByFounder] bit NOT NULL,
    [CancelledAt] datetime2 NULL,
    [CancelReason] nvarchar(400) NULL,
    [BillingEmail] nvarchar(400) NULL,
    [ContactPhone] nvarchar(400) NULL,
    [Notes] nvarchar(2000) NULL,
    [RequestedPlanId] uniqueidentifier NULL,
    [RequestedCycle] nvarchar(40) NULL,
    [RequestNote] nvarchar(400) NULL,
    [RequestedAt] datetime2 NULL,
    [LastReminderKey] nvarchar(60) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_WorkspaceSubscriptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkspaceSubscriptions_Organizations_OrgId] FOREIGN KEY ([OrgId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkspaceSubscriptions_SubscriptionPlans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [SubscriptionPlans] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SubscriptionInvoices] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(40) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [SubscriptionId] uniqueidentifier NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [Description] nvarchar(400) NOT NULL,
    [PeriodStart] date NULL,
    [PeriodEnd] date NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    [TaxPercent] decimal(5,2) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [Currency] nchar(3) NOT NULL,
    [IssuedOn] date NOT NULL,
    [DueOn] date NOT NULL,
    [PaidOn] date NULL,
    [PaymentMethod] nvarchar(400) NULL,
    [PaymentReference] nvarchar(400) NULL,
    [VoidReason] nvarchar(400) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SubscriptionInvoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SubscriptionInvoices_Organizations_OrgId] FOREIGN KEY ([OrgId]) REFERENCES [Organizations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SubscriptionInvoices_WorkspaceSubscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [WorkspaceSubscriptions] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_PlatformActivities_At] ON [PlatformActivities] ([At]);
GO

CREATE INDEX [IX_PlatformActivities_OrgId] ON [PlatformActivities] ([OrgId]);
GO

CREATE INDEX [IX_SignupRequests_PlanId] ON [SignupRequests] ([PlanId]);
GO

CREATE INDEX [IX_SignupRequests_SignInKey] ON [SignupRequests] ([SignInKey]);
GO

CREATE INDEX [IX_SignupRequests_Status] ON [SignupRequests] ([Status]);
GO

CREATE INDEX [IX_SignupRequests_VerificationTokenHash] ON [SignupRequests] ([VerificationTokenHash]);
GO

CREATE UNIQUE INDEX [IX_SubscriptionInvoices_Number] ON [SubscriptionInvoices] ([Number]);
GO

CREATE INDEX [IX_SubscriptionInvoices_OrgId_Status] ON [SubscriptionInvoices] ([OrgId], [Status]);
GO

CREATE INDEX [IX_SubscriptionInvoices_SubscriptionId] ON [SubscriptionInvoices] ([SubscriptionId]);
GO

CREATE UNIQUE INDEX [IX_SubscriptionPlans_Code] ON [SubscriptionPlans] ([Code]);
GO

CREATE UNIQUE INDEX [IX_WorkspaceSubscriptions_OrgId] ON [WorkspaceSubscriptions] ([OrgId]);
GO

CREATE INDEX [IX_WorkspaceSubscriptions_PlanId] ON [WorkspaceSubscriptions] ([PlanId]);
GO

CREATE INDEX [IX_WorkspaceSubscriptions_Status] ON [WorkspaceSubscriptions] ([Status]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105175521_PlatformConsole', N'8.0.11');
GO

COMMIT;
GO

