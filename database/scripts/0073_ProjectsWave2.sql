BEGIN TRANSACTION;
GO

ALTER TABLE [prj_Timesheets] ADD [ApprovalStatus] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [prj_Timesheets] ADD [CostAmount] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [prj_Timesheets] ADD [CostRate] decimal(19,4) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [prj_Timesheets] ADD [DecidedAt] datetime2 NULL;
GO

ALTER TABLE [prj_Timesheets] ADD [DecidedByUserId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Timesheets] ADD [RejectReason] nvarchar(300) NULL;
GO

ALTER TABLE [prj_Timesheets] ADD [SubmittedAt] datetime2 NULL;
GO

ALTER TABLE [prj_Timesheets] ADD [WorkerUserId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Timesheets] ADD [WrittenOff] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [prj_Tasks] ADD [AssignedUserId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Tasks] ADD [Labels] nvarchar(300) NULL;
GO

ALTER TABLE [prj_Tasks] ADD [ParentTaskId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Tasks] ADD [StartDate] date NULL;
GO

ALTER TABLE [prj_Projects] ADD [BudgetHours] decimal(9,2) NOT NULL DEFAULT 0.0;
GO

ALTER TABLE [prj_Projects] ADD [IsArchived] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [prj_Projects] ADD [IsTemplate] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [prj_Projects] ADD [ManagerUserId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Projects] ADD [SourceId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Projects] ADD [SourceType] nvarchar(60) NULL;
GO

ALTER TABLE [prj_Projects] ADD [TaxCodeId] uniqueidentifier NULL;
GO

ALTER TABLE [prj_Milestones] ADD [InvoiceId] uniqueidentifier NULL;
GO

CREATE TABLE [prj_Allocations] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [UserName] nvarchar(150) NOT NULL,
    [FromDate] date NOT NULL,
    [ToDate] date NOT NULL,
    [HoursPerWeek] decimal(9,2) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Allocations] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_ChangeOrders] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Number] nvarchar(30) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [AmountChange] decimal(19,4) NOT NULL,
    [HoursChange] decimal(9,2) NOT NULL,
    [Status] int NOT NULL,
    [DecidedAt] datetime2 NULL,
    [DecisionNote] nvarchar(500) NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_ChangeOrders] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_Comments] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerType] nvarchar(20) NOT NULL,
    [OwnerId] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [AuthorName] nvarchar(150) NOT NULL,
    [Text] nvarchar(2000) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Comments] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_Expenses] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [Category] nvarchar(60) NOT NULL,
    [Description] nvarchar(500) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [IsBillable] bit NOT NULL,
    [MarkupPercent] decimal(9,2) NOT NULL,
    [WorkerUserId] uniqueidentifier NULL,
    [WorkerName] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [RejectReason] nvarchar(300) NULL,
    [IsBilled] bit NOT NULL,
    [InvoiceId] uniqueidentifier NULL,
    [WrittenOff] bit NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Expenses] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_Members] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Role] nvarchar(100) NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Members] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_ProgressBillings] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Date] date NOT NULL,
    [Percent] decimal(9,4) NOT NULL,
    [Amount] decimal(19,4) NOT NULL,
    [InvoiceId] uniqueidentifier NULL,
    [Released] bit NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_ProgressBillings] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_Rates] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NULL,
    [UserId] uniqueidentifier NULL,
    [Role] nvarchar(100) NULL,
    [BillRate] decimal(19,4) NOT NULL,
    [CostRate] decimal(19,4) NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Rates] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_Risks] (
    [Id] uniqueidentifier NOT NULL,
    [ProjectId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Notes] nvarchar(2000) NULL,
    [Severity] int NOT NULL,
    [Status] int NOT NULL,
    [OwnerUserId] uniqueidentifier NULL,
    [OwnerName] nvarchar(150) NULL,
    [DueDate] date NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_Risks] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [prj_TaskDependencies] (
    [Id] uniqueidentifier NOT NULL,
    [TaskId] uniqueidentifier NOT NULL,
    [DependsOnTaskId] uniqueidentifier NOT NULL,
    [OrgId] uniqueidentifier NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] uniqueidentifier NULL,
    [RowVersion] rowversion NULL,
    CONSTRAINT [PK_prj_TaskDependencies] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_prj_Timesheets_OrgId_WorkerUserId_Date] ON [prj_Timesheets] ([OrgId], [WorkerUserId], [Date]);
GO

CREATE INDEX [IX_prj_Tasks_OrgId_AssignedUserId] ON [prj_Tasks] ([OrgId], [AssignedUserId]);
GO

CREATE INDEX [IX_prj_Tasks_OrgId_ParentTaskId] ON [prj_Tasks] ([OrgId], [ParentTaskId]);
GO

CREATE INDEX [IX_prj_Allocations_OrgId_UserId_FromDate] ON [prj_Allocations] ([OrgId], [UserId], [FromDate]);
GO

CREATE INDEX [IX_prj_ChangeOrders_OrgId_ProjectId] ON [prj_ChangeOrders] ([OrgId], [ProjectId]);
GO

CREATE INDEX [IX_prj_Comments_OrgId_OwnerType_OwnerId] ON [prj_Comments] ([OrgId], [OwnerType], [OwnerId]);
GO

CREATE INDEX [IX_prj_Expenses_OrgId_ProjectId_IsBilled] ON [prj_Expenses] ([OrgId], [ProjectId], [IsBilled]);
GO

CREATE UNIQUE INDEX [IX_prj_Members_OrgId_ProjectId_UserId] ON [prj_Members] ([OrgId], [ProjectId], [UserId]) WHERE [IsDeleted] = 0;
GO

CREATE INDEX [IX_prj_ProgressBillings_OrgId_ProjectId] ON [prj_ProgressBillings] ([OrgId], [ProjectId]);
GO

CREATE INDEX [IX_prj_Rates_OrgId_ProjectId_UserId] ON [prj_Rates] ([OrgId], [ProjectId], [UserId]);
GO

CREATE INDEX [IX_prj_Risks_OrgId_ProjectId_Status] ON [prj_Risks] ([OrgId], [ProjectId], [Status]);
GO

CREATE UNIQUE INDEX [IX_prj_TaskDependencies_OrgId_TaskId_DependsOnTaskId] ON [prj_TaskDependencies] ([OrgId], [TaskId], [DependsOnTaskId]) WHERE [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105095738_ProjectsWave2', N'8.0.11');
GO

COMMIT;
GO

