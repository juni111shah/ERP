BEGIN TRANSACTION;
GO

ALTER TABLE [re_WorkOrders] ADD [BillId] uniqueidentifier NULL;
GO

ALTER TABLE [re_WorkOrders] ADD [ChargeableTo] int NULL;
GO

ALTER TABLE [re_WorkOrders] ADD [OwnerSharePercent] decimal(9,4) NOT NULL DEFAULT 100.0;
GO

CREATE INDEX [IX_re_WorkOrders_OrgId_BillId] ON [re_WorkOrders] ([OrgId], [BillId]) WHERE [BillId] IS NOT NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029201608_OwnerCostBasis', N'8.0.11');
GO

COMMIT;
GO

