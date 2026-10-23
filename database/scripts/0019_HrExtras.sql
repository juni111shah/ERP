BEGIN TRANSACTION;
GO

ALTER TABLE [hr_Employees] ADD [BankRoutingCode] nvarchar(20) NULL;
GO

ALTER TABLE [hr_Employees] ADD [LabourCardNumber] nvarchar(30) NULL;
GO

CREATE TABLE [hr_ReviewCycles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [PeriodStart] date NOT NULL,
    [PeriodEnd] date NOT NULL,
    [Status] int NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_ReviewCycles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_PerformanceReviews] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [CycleId] uniqueidentifier NOT NULL,
    [ReviewerName] nvarchar(150) NULL,
    [OverallRating] int NULL,
    [Goals] nvarchar(4000) NULL,
    [Comments] nvarchar(4000) NULL,
    [Status] int NOT NULL,
    [SubmittedAt] datetime2 NULL,
    [AcknowledgedAt] datetime2 NULL,
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
    CONSTRAINT [PK_hr_PerformanceReviews] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_PerformanceReviews_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_hr_PerformanceReviews_hr_ReviewCycles_CycleId] FOREIGN KEY ([CycleId]) REFERENCES [hr_ReviewCycles] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_ReviewCompetencies] (
    [Id] uniqueidentifier NOT NULL,
    [ReviewId] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Score] int NOT NULL,
    [Notes] nvarchar(1000) NULL,
    CONSTRAINT [PK_hr_ReviewCompetencies] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_ReviewCompetencies_hr_PerformanceReviews_ReviewId] FOREIGN KEY ([ReviewId]) REFERENCES [hr_PerformanceReviews] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_hr_PerformanceReviews_CycleId] ON [hr_PerformanceReviews] ([CycleId]);
GO

CREATE INDEX [IX_hr_PerformanceReviews_EmployeeId] ON [hr_PerformanceReviews] ([EmployeeId]);
GO

CREATE UNIQUE INDEX [IX_hr_PerformanceReviews_OrgId_EmployeeId_CycleId] ON [hr_PerformanceReviews] ([OrgId], [EmployeeId], [CycleId]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_ReviewCompetencies_ReviewId] ON [hr_ReviewCompetencies] ([ReviewId]);
GO

CREATE UNIQUE INDEX [IX_hr_ReviewCycles_OrgId_Name] ON [hr_ReviewCycles] ([OrgId], [Name]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261023053134_HrExtras', N'8.0.11');
GO

COMMIT;
GO

