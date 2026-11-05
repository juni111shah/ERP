BEGIN TRANSACTION;
GO

ALTER TABLE [hr_Employees] ADD [ShiftId] uniqueidentifier NULL;
GO

CREATE TABLE [hr_Shifts] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [StartTime] time NOT NULL,
    [GraceMinutes] int NOT NULL,
    [HoursPerDay] int NOT NULL,
    [IsActive] bit NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_Shifts] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_hr_Shifts_OrgId_Code] ON [hr_Shifts] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105034402_HrShifts', N'8.0.11');
GO

COMMIT;
GO

