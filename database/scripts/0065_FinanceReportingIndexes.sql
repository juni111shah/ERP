BEGIN TRANSACTION;
GO

CREATE INDEX [IX_Settlements_OrgId_LegalEntityId_Direction_Date] ON [Settlements] ([OrgId], [LegalEntityId], [Direction], [Date]);
GO

CREATE INDEX [IX_JournalEntries_OrgId_LegalEntityId_Status_Date] ON [JournalEntries] ([OrgId], [LegalEntityId], [Status], [Date]);
GO

CREATE INDEX [IX_Invoices_OrgId_LegalEntityId_Status_InvoiceDate] ON [Invoices] ([OrgId], [LegalEntityId], [Status], [InvoiceDate]);
GO

CREATE INDEX [IX_Bills_OrgId_LegalEntityId_Status_BillDate] ON [Bills] ([OrgId], [LegalEntityId], [Status], [BillDate]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030051854_FinanceReportingIndexes', N'8.0.11');
GO

COMMIT;
GO

