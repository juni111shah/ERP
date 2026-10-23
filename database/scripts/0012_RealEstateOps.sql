BEGIN TRANSACTION;
GO

CREATE TABLE [re_PmSchedules] (
    [Id] uniqueidentifier NOT NULL,
    [PropertyId] uniqueidentifier NOT NULL,
    [UnitId] uniqueidentifier NULL,
    [Title] nvarchar(300) NOT NULL,
    [Category] nvarchar(50) NULL,
    [Priority] int NOT NULL,
    [FrequencyMonths] int NOT NULL,
    [NextDue] date NOT NULL,
    [LeadDays] int NOT NULL,
    [EstimatedCost] decimal(19,4) NOT NULL,
    [IsActive] bit NOT NULL,
    [LastGeneratedOn] date NULL,
    [Notes] nvarchar(4000) NULL,
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
    CONSTRAINT [PK_re_PmSchedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_PmSchedules_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_re_PmSchedules_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_re_PmSchedules_OrgId_IsActive_NextDue] ON [re_PmSchedules] ([OrgId], [IsActive], [NextDue]);
GO

CREATE INDEX [IX_re_PmSchedules_PropertyId] ON [re_PmSchedules] ([PropertyId]);
GO

CREATE INDEX [IX_re_PmSchedules_UnitId] ON [re_PmSchedules] ([UnitId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023041637_RealEstateOps', N'8.0.11');
GO

COMMIT;
GO

