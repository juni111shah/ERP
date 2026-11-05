BEGIN TRANSACTION;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr_AttendanceRecords]') AND [c].[name] = N'UnpaidAbsentDays');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [hr_AttendanceRecords] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [hr_AttendanceRecords] ALTER COLUMN [UnpaidAbsentDays] decimal(18,2) NOT NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261105032751_HrHalfDay', N'8.0.11');
GO

COMMIT;
GO

