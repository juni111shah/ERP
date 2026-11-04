BEGIN TRANSACTION;
GO

DROP INDEX [IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month] ON [hr_PayrollRuns];
GO

ALTER TABLE [hr_PayrollRuns] ADD [GeneratedByUserId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_PayrollRuns] ADD [IncludeSalary] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [hr_PayrollRuns] ADD [Kind] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [hr_PayrollRuns] ADD [LastAdjustedByUserId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_PayrollRuns] ADD [PostedByUserId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_PayrollRuns] ADD [VoidReason] nvarchar(500) NULL;
GO

ALTER TABLE [hr_PayrollRuns] ADD [VoidedAt] datetime2 NULL;
GO

ALTER TABLE [hr_PayrollRuns] ADD [VoidedBy] uniqueidentifier NULL;
GO

ALTER TABLE [hr_PayrollLines] ADD [PaidOn] date NULL;
GO

ALTER TABLE [hr_PayrollLines] ADD [PaymentJournalId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_PayrollLines] ADD [ProratedDays] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [hr_LeaveTypes] ADD [AccrualMode] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [hr_LeaveTypes] ADD [ProRataFirstYear] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [hr_LeaveTypes] ADD [RequiresAttachmentAfterDays] int NULL;
GO

ALTER TABLE [hr_LeaveRequests] ADD [EndHalfDay] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [hr_LeaveRequests] ADD [StartHalfDay] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [hr_Employees] ADD [Address] nvarchar(500) NULL;
GO

ALTER TABLE [hr_Employees] ADD [BankName] nvarchar(150) NULL;
GO

ALTER TABLE [hr_Employees] ADD [ContractEndDate] date NULL;
GO

ALTER TABLE [hr_Employees] ADD [ContractType] int NULL;
GO

ALTER TABLE [hr_Employees] ADD [DateOfBirth] date NULL;
GO

ALTER TABLE [hr_Employees] ADD [DepartmentId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_Employees] ADD [EmergencyContactName] nvarchar(150) NULL;
GO

ALTER TABLE [hr_Employees] ADD [EmergencyContactPhone] nvarchar(50) NULL;
GO

ALTER TABLE [hr_Employees] ADD [Gender] nvarchar(30) NULL;
GO

ALTER TABLE [hr_Employees] ADD [ManagerEmployeeId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_Employees] ADD [MaritalStatus] nvarchar(30) NULL;
GO

ALTER TABLE [hr_Employees] ADD [NoticePeriodDays] int NULL;
GO

ALTER TABLE [hr_Employees] ADD [PositionId] uniqueidentifier NULL;
GO

ALTER TABLE [hr_Employees] ADD [ProbationEndDate] date NULL;
GO

ALTER TABLE [hr_Employees] ADD [UserId] uniqueidentifier NULL;
GO

CREATE TABLE [hr_Departments] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [ParentDepartmentId] uniqueidentifier NULL,
    [HeadEmployeeId] uniqueidentifier NULL,
    [BusinessUnitId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_hr_Departments] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_EmployeeDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Type] int NOT NULL,
    [Number] nvarchar(100) NULL,
    [IssueDate] date NULL,
    [ExpiryDate] date NULL,
    [IssuedBy] nvarchar(150) NULL,
    [Notes] nvarchar(1000) NULL,
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
    CONSTRAINT [PK_hr_EmployeeDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_EmployeeDocuments_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_LeaveEntitlementTiers] (
    [Id] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [FromServiceYears] decimal(9,2) NOT NULL,
    [Days] decimal(9,2) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_LeaveEntitlementTiers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_LeaveEntitlementTiers_hr_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [hr_LeaveTypes] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_LeaveRequestYearDays] (
    [Id] uniqueidentifier NOT NULL,
    [RequestId] uniqueidentifier NOT NULL,
    [Year] int NOT NULL,
    [Days] decimal(9,2) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_LeaveRequestYearDays] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_Positions] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Title] nvarchar(150) NOT NULL,
    [Grade] nvarchar(50) NULL,
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
    CONSTRAINT [PK_hr_Positions] PRIMARY KEY ([Id])
);
GO

CREATE UNIQUE INDEX [IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month] ON [hr_PayrollRuns] ([OrgId], [LegalEntityId], [Year], [Month]) WHERE [IsDeleted] = 0 AND [Kind] = 0 AND [Status] <> 4;
GO

CREATE INDEX [IX_hr_Employees_OrgId_DepartmentId] ON [hr_Employees] ([OrgId], [DepartmentId]);
GO

CREATE INDEX [IX_hr_Employees_OrgId_ManagerEmployeeId] ON [hr_Employees] ([OrgId], [ManagerEmployeeId]);
GO

CREATE UNIQUE INDEX [IX_hr_Employees_OrgId_UserId] ON [hr_Employees] ([OrgId], [UserId]) WHERE [UserId] IS NOT NULL AND [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [IX_hr_Departments_OrgId_Code] ON [hr_Departments] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_EmployeeDocuments_EmployeeId] ON [hr_EmployeeDocuments] ([EmployeeId]);
GO

CREATE INDEX [IX_hr_EmployeeDocuments_OrgId_EmployeeId] ON [hr_EmployeeDocuments] ([OrgId], [EmployeeId]);
GO

CREATE INDEX [IX_hr_EmployeeDocuments_OrgId_ExpiryDate] ON [hr_EmployeeDocuments] ([OrgId], [ExpiryDate]);
GO

CREATE INDEX [IX_hr_LeaveEntitlementTiers_LeaveTypeId] ON [hr_LeaveEntitlementTiers] ([LeaveTypeId]);
GO

CREATE INDEX [IX_hr_LeaveEntitlementTiers_OrgId_LeaveTypeId] ON [hr_LeaveEntitlementTiers] ([OrgId], [LeaveTypeId]);
GO

CREATE INDEX [IX_hr_LeaveRequestYearDays_OrgId_RequestId] ON [hr_LeaveRequestYearDays] ([OrgId], [RequestId]);
GO

CREATE UNIQUE INDEX [IX_hr_Positions_OrgId_Code] ON [hr_Positions] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO


UPDATE l SET ProratedDays = DAY(EOMONTH(DATEFROMPARTS(r.[Year], r.[Month], 1)))
FROM hr_PayrollLines l JOIN hr_PayrollRuns r ON r.Id = l.PayrollRunId
WHERE l.ProratedDays = 0;
GO


UPDATE l SET PaidOn = r.PaidOn, PaymentJournalId = r.PaymentJournalId
FROM hr_PayrollLines l JOIN hr_PayrollRuns r ON r.Id = l.PayrollRunId
WHERE r.[Status] = 3 AND l.PaidOn IS NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104110602_HrCompletion', N'8.0.11');
GO

COMMIT;
GO

