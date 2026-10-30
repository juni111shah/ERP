BEGIN TRANSACTION;
GO

CREATE TABLE [inv_StockCounts] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [CountDate] date NOT NULL,
    [Status] int NOT NULL,
    [Memo] nvarchar(500) NULL,
    [PostedAt] datetime2 NULL,
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
    CONSTRAINT [PK_inv_StockCounts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_inv_StockCounts_inv_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [inv_Warehouses] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [inv_StockCountLines] (
    [Id] uniqueidentifier NOT NULL,
    [StockCountId] uniqueidentifier NOT NULL,
    [ItemId] uniqueidentifier NOT NULL,
    [SystemQuantity] decimal(19,4) NOT NULL,
    [CountedQuantity] decimal(19,4) NULL,
    [MovementId] uniqueidentifier NULL,
    CONSTRAINT [PK_inv_StockCountLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_inv_StockCountLines_Items_ItemId] FOREIGN KEY ([ItemId]) REFERENCES [Items] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_inv_StockCountLines_inv_StockCounts_StockCountId] FOREIGN KEY ([StockCountId]) REFERENCES [inv_StockCounts] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_inv_StockCountLines_ItemId] ON [inv_StockCountLines] ([ItemId]);
GO

CREATE UNIQUE INDEX [IX_inv_StockCountLines_StockCountId_ItemId] ON [inv_StockCountLines] ([StockCountId], [ItemId]);
GO

CREATE UNIQUE INDEX [IX_inv_StockCounts_OrgId_Number] ON [inv_StockCounts] ([OrgId], [Number]);
GO

CREATE INDEX [IX_inv_StockCounts_OrgId_WarehouseId_CountDate] ON [inv_StockCounts] ([OrgId], [WarehouseId], [CountDate]);
GO

CREATE INDEX [IX_inv_StockCounts_WarehouseId] ON [inv_StockCounts] ([WarehouseId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029154404_InventoryCounts', N'8.0.11');
GO

COMMIT;
GO

