BEGIN TRANSACTION;
GO

ALTER TABLE [hr_PayrollLines] ADD [EndOfService] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [hr_PayrollLines] ADD [LeaveEncashment] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [hr_PayrollLines] ADD [LoanRecovery] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [hr_LeaveTypes] ADD [Encashable] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [hr_Employees] ADD [LastWorkingDay] date NULL;
GO

ALTER TABLE [hr_Employees] ADD [NoticeGivenOn] date NULL;
GO

ALTER TABLE [hr_Employees] ADD [TerminationReason] nvarchar(300) NULL;
GO

CREATE TABLE [hr_EmployeeLoans] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Principal] decimal(19,4) NOT NULL,
    [InstalmentAmount] decimal(19,4) NOT NULL,
    [StartYear] int NOT NULL,
    [StartMonth] int NOT NULL,
    [Instalments] int NOT NULL,
    [Remaining] decimal(19,4) NOT NULL,
    [Status] int NOT NULL,
    [Reason] nvarchar(300) NULL,
    [DisbursedOn] date NULL,
    [DisbursementJournalId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_hr_EmployeeLoans] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_EmployeeLoans_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_LeavePayBands] (
    [Id] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [FromDay] int NOT NULL,
    [PayPercent] decimal(5,2) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_LeavePayBands] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_LeavePayBands_hr_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [hr_LeaveTypes] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_PayrollLoanRecoveries] (
    [Id] uniqueidentifier NOT NULL,
    [PayrollRunId] uniqueidentifier NOT NULL,
    [PayrollLineId] uniqueidentifier NOT NULL,
    [LoanId] uniqueidentifier NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [Applied] bit NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_PayrollLoanRecoveries] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_PayrollLoanRecoveries_hr_EmployeeLoans_LoanId] FOREIGN KEY ([LoanId]) REFERENCES [hr_EmployeeLoans] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_hr_EmployeeLoans_EmployeeId] ON [hr_EmployeeLoans] ([EmployeeId]);
GO

CREATE INDEX [IX_hr_EmployeeLoans_OrgId_EmployeeId_Status] ON [hr_EmployeeLoans] ([OrgId], [EmployeeId], [Status]);
GO

CREATE INDEX [IX_hr_LeavePayBands_LeaveTypeId] ON [hr_LeavePayBands] ([LeaveTypeId]);
GO

CREATE INDEX [IX_hr_LeavePayBands_OrgId_LeaveTypeId] ON [hr_LeavePayBands] ([OrgId], [LeaveTypeId]);
GO

CREATE INDEX [IX_hr_PayrollLoanRecoveries_LoanId] ON [hr_PayrollLoanRecoveries] ([LoanId]);
GO

CREATE INDEX [IX_hr_PayrollLoanRecoveries_OrgId_LoanId] ON [hr_PayrollLoanRecoveries] ([OrgId], [LoanId]);
GO

CREATE INDEX [IX_hr_PayrollLoanRecoveries_OrgId_PayrollRunId] ON [hr_PayrollLoanRecoveries] ([OrgId], [PayrollRunId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104174839_HrWave1', N'8.0.11');
GO

COMMIT;
GO

