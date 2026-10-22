BEGIN TRANSACTION;
GO

CREATE TABLE [hr_Employees] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [FullName] nvarchar(200) NOT NULL,
    [Designation] nvarchar(150) NOT NULL,
    [Department] nvarchar(100) NULL,
    [JoinDate] date NOT NULL,
    [TerminationDate] date NULL,
    [Email] nvarchar(200) NULL,
    [Phone] nvarchar(50) NULL,
    [Nationality] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [BasicSalary] decimal(19,4) NOT NULL,
    [HousingAllowance] decimal(19,4) NOT NULL,
    [TransportAllowance] decimal(19,4) NOT NULL,
    [OtherAllowances] decimal(19,4) NOT NULL,
    [RecurringDeductions] decimal(19,4) NOT NULL,
    [Iban] nvarchar(50) NULL,
    [CustomFields] nvarchar(max) NULL,
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
    CONSTRAINT [PK_hr_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_hr_Employees_CustomFields_IsJson] CHECK ([CustomFields] IS NULL OR ISJSON([CustomFields]) = 1)
);
GO

CREATE TABLE [hr_PayrollRuns] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [PlannedPayDate] date NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(500) NULL,
    [Employees] int NOT NULL,
    [GrossEarnings] decimal(19,4) NOT NULL,
    [Deductions] decimal(19,4) NOT NULL,
    [NetPay] decimal(19,4) NOT NULL,
    [ApprovalRequestId] uniqueidentifier NULL,
    [AccrualJournalId] uniqueidentifier NULL,
    [PaymentJournalId] uniqueidentifier NULL,
    [PaidOn] date NULL,
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
    CONSTRAINT [PK_hr_PayrollRuns] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_AttendanceRecords] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Year] int NOT NULL,
    [Month] int NOT NULL,
    [WorkingDays] int NOT NULL,
    [DaysPresent] int NOT NULL,
    [UnpaidAbsentDays] int NOT NULL,
    [PaidLeaveDays] int NOT NULL,
    [OvertimeHours] decimal(9,2) NOT NULL,
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
    CONSTRAINT [PK_hr_AttendanceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_AttendanceRecords_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_PayrollLines] (
    [Id] uniqueidentifier NOT NULL,
    [PayrollRunId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeName] nvarchar(200) NOT NULL,
    [EmployeeNumber] nvarchar(30) NULL,
    [AbsentDays] int NOT NULL,
    [OvertimeHours] decimal(9,2) NOT NULL,
    [Basic] decimal(19,4) NOT NULL,
    [Allowances] decimal(19,4) NOT NULL,
    [Overtime] decimal(19,4) NOT NULL,
    [Absence] decimal(19,4) NOT NULL,
    [Adjustment] decimal(19,4) NOT NULL,
    [AdjustmentNote] nvarchar(300) NULL,
    [GrossEarnings] decimal(19,4) NOT NULL,
    [Deductions] decimal(19,4) NOT NULL,
    [NetPay] decimal(19,4) NOT NULL,
    CONSTRAINT [PK_hr_PayrollLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_PayrollLines_hr_PayrollRuns_PayrollRunId] FOREIGN KEY ([PayrollRunId]) REFERENCES [hr_PayrollRuns] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_hr_AttendanceRecords_EmployeeId] ON [hr_AttendanceRecords] ([EmployeeId]);
GO

CREATE UNIQUE INDEX [IX_hr_AttendanceRecords_OrgId_EmployeeId_Year_Month] ON [hr_AttendanceRecords] ([OrgId], [EmployeeId], [Year], [Month]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_Employees_OrgId_LegalEntityId_Status] ON [hr_Employees] ([OrgId], [LegalEntityId], [Status]);
GO

CREATE UNIQUE INDEX [IX_hr_Employees_OrgId_Number] ON [hr_Employees] ([OrgId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_PayrollLines_PayrollRunId] ON [hr_PayrollLines] ([PayrollRunId]);
GO

CREATE UNIQUE INDEX [IX_hr_PayrollRuns_OrgId_LegalEntityId_Number] ON [hr_PayrollRuns] ([OrgId], [LegalEntityId], [Number]) WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month] ON [hr_PayrollRuns] ([OrgId], [LegalEntityId], [Year], [Month]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022202642_HrModule', N'8.0.11');
GO

COMMIT;
GO

