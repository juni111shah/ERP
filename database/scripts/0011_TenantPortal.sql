BEGIN TRANSACTION;
GO

CREATE TABLE [PortalOtps] (
    [Id] uniqueidentifier NOT NULL,
    [PartyId] uniqueidentifier NOT NULL,
    [CodeHash] nvarchar(100) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [Attempts] int NOT NULL,
    [UsedAt] datetime2 NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_PortalOtps] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_PortalOtps_OrgId_PartyId] ON [PortalOtps] ([OrgId], [PartyId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023040602_TenantPortal', N'8.0.11');
GO

COMMIT;
GO

