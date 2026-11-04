BEGIN TRANSACTION;
GO

ALTER TABLE [hr_PayrollRuns] ADD [EmployerCost] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [hr_PayrollLines] ADD [EmployerCost] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [hr_PayrollLines] ADD [StatutoryDeduction] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

CREATE TABLE [hr_AttendanceLogs] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [CheckIn] time NULL,
    [CheckOut] time NULL,
    [Status] int NOT NULL,
    [WorkedMinutes] int NOT NULL,
    [LateMinutes] int NOT NULL,
    [OvertimeMinutes] int NOT NULL,
    [Notes] nvarchar(300) NULL,
    [Source] nvarchar(50) NULL,
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
    CONSTRAINT [PK_hr_AttendanceLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_AttendanceLogs_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_ChecklistTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Kind] int NOT NULL,
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
    CONSTRAINT [PK_hr_ChecklistTemplates] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_EmployeeHistory] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [EffectiveDate] date NOT NULL,
    [Kind] int NOT NULL,
    [Summary] nvarchar(500) NOT NULL,
    [OldValue] nvarchar(300) NULL,
    [NewValue] nvarchar(300) NULL,
    [IsPay] bit NOT NULL,
    [ChangedByUserId] uniqueidentifier NULL,
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
    CONSTRAINT [PK_hr_EmployeeHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_EmployeeHistory_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_EmployeeTasks] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [DueDate] date NULL,
    [IsDone] bit NOT NULL,
    [DoneAt] datetime2 NULL,
    [DoneByUserId] uniqueidentifier NULL,
    [Notes] nvarchar(500) NULL,
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
    CONSTRAINT [PK_hr_EmployeeTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_EmployeeTasks_hr_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [hr_Employees] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_JobOpenings] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(150) NOT NULL,
    [DepartmentId] uniqueidentifier NULL,
    [PositionId] uniqueidentifier NULL,
    [Headcount] int NOT NULL,
    [Status] int NOT NULL,
    [Description] nvarchar(4000) NULL,
    [ClosingDate] date NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_JobOpenings] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_PayrollLineStatutory] (
    [Id] uniqueidentifier NOT NULL,
    [PayrollRunId] uniqueidentifier NOT NULL,
    [PayrollLineId] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [RuleId] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Kind] int NOT NULL,
    [BaseAmount] decimal(19,4) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_PayrollLineStatutory] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_StatutoryRules] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(20) NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Kind] int NOT NULL,
    [Method] int NOT NULL,
    [Base] int NOT NULL,
    [Rate] decimal(19,4) NOT NULL,
    [Threshold] decimal(19,4) NOT NULL,
    [Cap] decimal(19,4) NOT NULL,
    [Nationalities] nvarchar(300) NULL,
    [EffectiveFrom] date NULL,
    [EffectiveTo] date NULL,
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
    [LegalEntityId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_hr_StatutoryRules] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [hr_ChecklistTemplateItems] (
    [Id] uniqueidentifier NOT NULL,
    [TemplateId] uniqueidentifier NOT NULL,
    [Position] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [DueOffsetDays] int NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_ChecklistTemplateItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_ChecklistTemplateItems_hr_ChecklistTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [hr_ChecklistTemplates] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_Candidates] (
    [Id] uniqueidentifier NOT NULL,
    [OpeningId] uniqueidentifier NOT NULL,
    [FullName] nvarchar(200) NOT NULL,
    [Email] nvarchar(200) NULL,
    [Phone] nvarchar(50) NULL,
    [Stage] int NOT NULL,
    [Source] nvarchar(100) NULL,
    [ExpectedSalary] decimal(19,4) NULL,
    [Notes] nvarchar(2000) NULL,
    [StageChangedAt] datetime2 NOT NULL,
    [EmployeeId] uniqueidentifier NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_Candidates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_Candidates_hr_JobOpenings_OpeningId] FOREIGN KEY ([OpeningId]) REFERENCES [hr_JobOpenings] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [hr_StatutorySlabs] (
    [Id] uniqueidentifier NOT NULL,
    [RuleId] uniqueidentifier NOT NULL,
    [FromAmount] decimal(19,4) NOT NULL,
    [Rate] decimal(9,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_hr_StatutorySlabs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_hr_StatutorySlabs_hr_StatutoryRules_RuleId] FOREIGN KEY ([RuleId]) REFERENCES [hr_StatutoryRules] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_hr_AttendanceLogs_EmployeeId] ON [hr_AttendanceLogs] ([EmployeeId]);
GO

CREATE UNIQUE INDEX [IX_hr_AttendanceLogs_OrgId_EmployeeId_Date] ON [hr_AttendanceLogs] ([OrgId], [EmployeeId], [Date]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_AttendanceLogs_OrgId_LegalEntityId_Date] ON [hr_AttendanceLogs] ([OrgId], [LegalEntityId], [Date]);
GO

CREATE INDEX [IX_hr_Candidates_OpeningId] ON [hr_Candidates] ([OpeningId]);
GO

CREATE INDEX [IX_hr_Candidates_OrgId_OpeningId_Stage] ON [hr_Candidates] ([OrgId], [OpeningId], [Stage]);
GO

CREATE INDEX [IX_hr_ChecklistTemplateItems_OrgId_TemplateId] ON [hr_ChecklistTemplateItems] ([OrgId], [TemplateId]);
GO

CREATE INDEX [IX_hr_ChecklistTemplateItems_TemplateId] ON [hr_ChecklistTemplateItems] ([TemplateId]);
GO

CREATE UNIQUE INDEX [IX_hr_ChecklistTemplates_OrgId_Name] ON [hr_ChecklistTemplates] ([OrgId], [Name]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_EmployeeHistory_EmployeeId] ON [hr_EmployeeHistory] ([EmployeeId]);
GO

CREATE INDEX [IX_hr_EmployeeHistory_OrgId_EmployeeId_EffectiveDate] ON [hr_EmployeeHistory] ([OrgId], [EmployeeId], [EffectiveDate]);
GO

CREATE INDEX [IX_hr_EmployeeTasks_EmployeeId] ON [hr_EmployeeTasks] ([EmployeeId]);
GO

CREATE INDEX [IX_hr_EmployeeTasks_OrgId_EmployeeId] ON [hr_EmployeeTasks] ([OrgId], [EmployeeId]);
GO

CREATE INDEX [IX_hr_EmployeeTasks_OrgId_IsDone_DueDate] ON [hr_EmployeeTasks] ([OrgId], [IsDone], [DueDate]);
GO

CREATE INDEX [IX_hr_JobOpenings_OrgId_Status] ON [hr_JobOpenings] ([OrgId], [Status]);
GO

CREATE INDEX [IX_hr_PayrollLineStatutory_OrgId_PayrollRunId] ON [hr_PayrollLineStatutory] ([OrgId], [PayrollRunId]);
GO

CREATE INDEX [IX_hr_PayrollLineStatutory_OrgId_RuleId] ON [hr_PayrollLineStatutory] ([OrgId], [RuleId]);
GO

CREATE UNIQUE INDEX [IX_hr_StatutoryRules_OrgId_LegalEntityId_Code] ON [hr_StatutoryRules] ([OrgId], [LegalEntityId], [Code]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_hr_StatutorySlabs_OrgId_RuleId] ON [hr_StatutorySlabs] ([OrgId], [RuleId]);
GO

CREATE INDEX [IX_hr_StatutorySlabs_RuleId] ON [hr_StatutorySlabs] ([RuleId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261104183934_HrWave4', N'8.0.11');
GO

COMMIT;
GO

