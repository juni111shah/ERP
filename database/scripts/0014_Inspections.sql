BEGIN TRANSACTION;
GO

CREATE TABLE [re_Inspections] (
    [Id] uniqueidentifier NOT NULL,
    [PropertyId] uniqueidentifier NOT NULL,
    [UnitId] uniqueidentifier NULL,
    [LeaseId] uniqueidentifier NULL,
    [Kind] int NOT NULL,
    [InspectionDate] date NOT NULL,
    [Inspector] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(4000) NULL,
    [DamageCost] decimal(19,4) NOT NULL,
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
    CONSTRAINT [PK_re_Inspections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_Inspections_re_Leases_LeaseId] FOREIGN KEY ([LeaseId]) REFERENCES [re_Leases] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_re_Inspections_re_Properties_PropertyId] FOREIGN KEY ([PropertyId]) REFERENCES [re_Properties] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_re_Inspections_re_Units_UnitId] FOREIGN KEY ([UnitId]) REFERENCES [re_Units] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [re_InspectionItems] (
    [Id] uniqueidentifier NOT NULL,
    [InspectionId] uniqueidentifier NOT NULL,
    [Area] nvarchar(100) NOT NULL,
    [Condition] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [RepairCost] decimal(19,4) NOT NULL,
    [WorkOrderId] uniqueidentifier NULL,
    CONSTRAINT [PK_re_InspectionItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_re_InspectionItems_re_Inspections_InspectionId] FOREIGN KEY ([InspectionId]) REFERENCES [re_Inspections] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_re_InspectionItems_InspectionId] ON [re_InspectionItems] ([InspectionId]);
GO

CREATE INDEX [IX_re_Inspections_LeaseId] ON [re_Inspections] ([LeaseId]);
GO

CREATE INDEX [IX_re_Inspections_OrgId_PropertyId_InspectionDate] ON [re_Inspections] ([OrgId], [PropertyId], [InspectionDate]);
GO

CREATE INDEX [IX_re_Inspections_PropertyId] ON [re_Inspections] ([PropertyId]);
GO

CREATE INDEX [IX_re_Inspections_UnitId] ON [re_Inspections] ([UnitId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023045610_Inspections', N'8.0.11');
GO

COMMIT;
GO

