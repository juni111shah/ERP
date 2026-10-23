BEGIN TRANSACTION;
GO

CREATE TABLE [inv_StockMovements] (
    [Id] uniqueidentifier NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [ItemId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [Kind] int NOT NULL,
    [Quantity] decimal(19,4) NOT NULL,
    [UnitCost] decimal(19,4) NOT NULL,
    [Reference] nvarchar(100) NULL,
    [Memo] nvarchar(500) NULL,
    [JournalEntryId] uniqueidentifier NULL,
    [TransferId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_inv_StockMovements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_inv_StockMovements_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_inv_StockMovements_inv_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [inv_Warehouses] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [inv_StockPolicies] (
    [Id] uniqueidentifier NOT NULL,
    [ItemId] uniqueidentifier NOT NULL,
    [ReorderLevel] decimal(19,4) NOT NULL,
    [ReorderQuantity] decimal(19,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_inv_StockPolicies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_inv_StockPolicies_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_inv_StockMovements_ItemId] ON [inv_StockMovements] ([ItemId]);
GO

CREATE INDEX [IX_inv_StockMovements_OrgId_WarehouseId_ItemId_Date] ON [inv_StockMovements] ([OrgId], [WarehouseId], [ItemId], [Date]);
GO

CREATE INDEX [IX_inv_StockMovements_WarehouseId] ON [inv_StockMovements] ([WarehouseId]);
GO

CREATE INDEX [IX_inv_StockPolicies_ItemId] ON [inv_StockPolicies] ([ItemId]);
GO

CREATE UNIQUE INDEX [IX_inv_StockPolicies_OrgId_ItemId] ON [inv_StockPolicies] ([OrgId], [ItemId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023051929_InventoryStock', N'8.0.11');
GO

COMMIT;
GO

