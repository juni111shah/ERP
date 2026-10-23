BEGIN TRANSACTION;
GO

CREATE TABLE [re_Listings] (
    [Id] uniqueidentifier NOT NULL,
    [PropertyId] uniqueidentifier NOT NULL,
    [UnitId] uniqueidentifier NULL,
    [Kind] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Price] decimal(19,4) NOT NULL,
    [Status] int NOT NULL,
    [Channels] nvarchar(300) NULL,
    [Description] nvarchar(4000) NULL,
    [ListedOn] date NULL,
    [ClosedOn] date NULL,
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
    CONSTRAINT [PK_re_Listings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_Listings_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_re_Listings_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [re_Leads] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Phone] nvarchar(40) NULL,
    [Email] nvarchar(150) NULL,
    [Source] nvarchar(60) NULL,
    [Interest] nvarchar(300) NULL,
    [AgentPartyId] uniqueidentifier NULL,
    [Stage] int NOT NULL,
    [LostReason] nvarchar(300) NULL,
    [ListingId] uniqueidentifier NULL,
    [UnitId] uniqueidentifier NULL,
    [Notes] nvarchar(4000) NULL,
    [FollowUpOn] date NULL,
    [DealId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_re_Leads] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_Leads_re_Listings_ListingId] FOREIGN KEY ([ListingId]) REFERENCES [re_Listings] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_re_Leads_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_re_Leads_ListingId] ON [re_Leads] ([ListingId]);
GO

CREATE INDEX [IX_re_Leads_OrgId_FollowUpOn] ON [re_Leads] ([OrgId], [FollowUpOn]);
GO

CREATE INDEX [IX_re_Leads_OrgId_LegalEntityId_Stage] ON [re_Leads] ([OrgId], [LegalEntityId], [Stage]);
GO

CREATE INDEX [IX_re_Leads_UnitId] ON [re_Leads] ([UnitId]);
GO

CREATE INDEX [IX_re_Listings_OrgId_LegalEntityId_Status] ON [re_Listings] ([OrgId], [LegalEntityId], [Status]);
GO

CREATE INDEX [IX_re_Listings_PropertyId] ON [re_Listings] ([PropertyId]);
GO

CREATE INDEX [IX_re_Listings_UnitId] ON [re_Listings] ([UnitId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023052928_Crm', N'8.0.11');
GO

COMMIT;
GO

