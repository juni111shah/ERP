BEGIN TRANSACTION;
GO

CREATE TABLE [hr_ShiftRosters] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [ShiftId] uniqueidentifier NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
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
    CONSTRAINT [PK_hr_ShiftRosters] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_hr_ShiftRosters_OrgId_EmployeeId_FromDate] ON [hr_ShiftRosters] ([OrgId], [EmployeeId], [FromDate]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105040727_HrShiftRoster', N'8.0.11');
GO

COMMIT;
GO

