BEGIN TRANSACTION;
GO

CREATE TABLE [hr_LeaveTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [IsPaid] bit NOT NULL,
    [AnnualDays] decimal(9,2) NOT NULL,
    [CarryForwardCap] decimal(9,2) NOT NULL,
    [RequiresApproval] bit NOT NULL,
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
    CONSTRAINT [PK_hr_LeaveTypes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_PublicHolidays] (
    [Id] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_PublicHolidays] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_LeaveBalances] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [Year] int NOT NULL,
    [Entitled] decimal(9,2) NOT NULL,
    [Carried] decimal(9,2) NOT NULL,
    [Taken] decimal(9,2) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_LeaveBalances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_LeaveBalances_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_hr_LeaveBalances_hr_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [hr_LeaveTypes] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_LeaveRequests] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EmployeeName] nvarchar(200) NOT NULL,
    [LeaveTypeId] uniqueidentifier NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [Days] decimal(9,2) NOT NULL,
    [Status] int NOT NULL,
    [Reason] nvarchar(500) NULL,
    [RequestedByUserId] uniqueidentifier NULL,
    [SubmittedAt] datetime2 NULL,
    [DecidedByUserId] uniqueidentifier NULL,
    [DecidedAt] datetime2 NULL,
    [DecisionComment] nvarchar(500) NULL,
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
    CONSTRAINT [PK_hr_LeaveRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_LeaveRequests_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_hr_LeaveRequests_hr_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [hr_LeaveTypes] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_hr_LeaveBalances_EmployeeId] ON [hr_LeaveBalances] ([EmployeeId]);
GO

CREATE INDEX [IX_hr_LeaveBalances_LeaveTypeId] ON [hr_LeaveBalances] ([LeaveTypeId]);
GO

CREATE UNIQUE INDEX [IX_hr_LeaveBalances_OrgId_EmployeeId_LeaveTypeId_Year] ON [hr_LeaveBalances] ([OrgId], [EmployeeId], [LeaveTypeId], [Year]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_LeaveRequests_EmployeeId] ON [hr_LeaveRequests] ([EmployeeId]);
GO

CREATE INDEX [IX_hr_LeaveRequests_LeaveTypeId] ON [hr_LeaveRequests] ([LeaveTypeId]);
GO

CREATE INDEX [IX_hr_LeaveRequests_OrgId_EmployeeId_FromDate] ON [hr_LeaveRequests] ([OrgId], [EmployeeId], [FromDate]);
GO

CREATE INDEX [IX_hr_LeaveRequests_OrgId_Status] ON [hr_LeaveRequests] ([OrgId], [Status]);
GO

CREATE UNIQUE INDEX [IX_hr_LeaveTypes_OrgId_Code] ON [hr_LeaveTypes] ([OrgId], [Code]) WHERE [IsDeleted] = 0;
GO

CREATE UNIQUE INDEX [IX_hr_PublicHolidays_OrgId_Date] ON [hr_PublicHolidays] ([OrgId], [Date]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029160406_HrLeave', N'8.0.11');
GO

COMMIT;
GO

