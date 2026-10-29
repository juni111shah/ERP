BEGIN TRANSACTION;
GO

ALTER TABLE [PaymentInstruments] ADD [BouncedOn] date NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [FollowUpNote] nvarchar(500) NULL;
GO

ALTER TABLE [PaymentInstruments] ADD [FollowUpOn] date NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029202556_ChequeBounceFollowUp', N'8.0.11');
GO

COMMIT;
GO

