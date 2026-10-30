BEGIN TRANSACTION;
GO

CREATE UNIQUE INDEX [UX_pos_Sessions_OneOpenPerRegister] ON [pos_Sessions] ([OrgId], [RegisterId]) WHERE [Status] = 0 AND [IsDeleted] = 0;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261030045924_PosOneOpenSessionPerRegister', N'8.0.11');
GO

COMMIT;
GO

