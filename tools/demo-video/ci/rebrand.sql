-- CI only, run after all seeding: the client video must not show the word "demo". The seeded workspaces become the Qatari
-- companies their legal entities already are (Al Rayyan Trading, Lusail Properties); the demo flag, sign-in keys, e-mail addresses
-- and demo-flavoured names and memos are rewritten in place. The database is thrown away when the run ends.
SET NOCOUNT ON;
SET XACT_ABORT OFF;

UPDATE Organizations SET Code = 'alrayyan' WHERE Code = 'demo';
UPDATE Organizations SET Code = 'lusail' WHERE Code = 'demo-re';
UPDATE Organizations SET IsDemo = 0;

-- Phrase replacements in every text column (longest phrases first; SQL Server's default collation matches case-insensitively).
DECLARE @pairs TABLE (n int IDENTITY, f nvarchar(100), t nvarchar(100));
INSERT @pairs (f, t) VALUES
  (N'DEMO TRADING W.L.L.', N'AL RAYYAN TRADING W.L.L.'),
  (N'@demotrading.local', N'@alrayyan.qa'),
  (N'demotrading.local', N'alrayyan.qa'),
  (N'@demo.local', N'@alrayyan.qa'),
  (N'Demo Workspace', N'Al Rayyan Trading'),
  (N'Demo Properties', N'Lusail Properties'),
  (N'Demo Administrator', N'Khalid Al Mansouri'),
  (N'Demo Accountant', N'Noura Hassan'),
  (N'Demo Leasing Agent', N'Rashid Ahmed'),
  (N'Demo Workshop', N'Product Workshop'),
  (N'Demo request', N'Purchase request'),
  (N'demo credit', N'customer credit'),
  (N'Demonstrated', N'Presented'),
  (N'Demo data', N'Sample data');

DECLARE @expr nvarchar(max), @col sysname, @tbl nvarchar(300), @sql nvarchar(max);
DECLARE cols CURSOR LOCAL FAST_FORWARD FOR
  SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(t.name), QUOTENAME(c.name)
  FROM sys.columns c
  JOIN sys.tables t ON t.object_id = c.object_id
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.types ty ON ty.user_type_id = c.user_type_id
  WHERE ty.name IN (N'nvarchar', N'varchar') AND c.is_computed = 0 AND t.is_ms_shipped = 0 AND t.temporal_type <> 1;
OPEN cols;
FETCH NEXT FROM cols INTO @tbl, @col;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @expr = @col;
  SELECT @expr = N'REPLACE(' + @expr + N', N''' + REPLACE(f, '''', '''''') + N''', N''' + REPLACE(t, '''', '''''') + N''')' FROM @pairs ORDER BY n;
  SET @sql = N'UPDATE ' + @tbl + N' SET ' + @col + N' = ' + @expr + N' WHERE ' + @col + N' LIKE N''%demo%'';';
  BEGIN TRY
    EXEC sp_executesql @sql;
    IF @@ROWCOUNT > 0 PRINT CONCAT(N'rebrand: ', @tbl, N'.', @col);
  END TRY
  BEGIN CATCH
    PRINT CONCAT(N'rebrand: skipped ', @tbl, N'.', @col, N': ', ERROR_MESSAGE());
  END CATCH;
  FETCH NEXT FROM cols INTO @tbl, @col;
END
CLOSE cols; DEALLOCATE cols;

-- Identity looks people up by the upper-cased copies.
UPDATE Users SET NormalizedEmail = UPPER(Email), NormalizedUserName = UPPER(UserName);

-- What still says "demo" (internal keys are expected here; anything a person would read is not).
CREATE TABLE #left (col nvarchar(300), hits int, sample nvarchar(200));
DECLARE cols2 CURSOR LOCAL FAST_FORWARD FOR
  SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(t.name), QUOTENAME(c.name)
  FROM sys.columns c
  JOIN sys.tables t ON t.object_id = c.object_id
  JOIN sys.schemas s ON s.schema_id = t.schema_id
  JOIN sys.types ty ON ty.user_type_id = c.user_type_id
  WHERE ty.name IN (N'nvarchar', N'varchar') AND c.is_computed = 0 AND t.is_ms_shipped = 0;
OPEN cols2;
FETCH NEXT FROM cols2 INTO @tbl, @col;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @sql = N'INSERT #left SELECT N''' + REPLACE(@tbl + N'.' + @col, '''', '''''') + N''', COUNT(*), LEFT(MIN(CAST(' + @col + N' AS nvarchar(max))), 200) FROM ' + @tbl + N' WHERE ' + @col + N' LIKE N''%demo%'' HAVING COUNT(*) > 0;';
  BEGIN TRY EXEC sp_executesql @sql; END TRY BEGIN CATCH END CATCH;
  FETCH NEXT FROM cols2 INTO @tbl, @col;
END
CLOSE cols2; DEALLOCATE cols2;
SELECT col, hits, sample FROM #left ORDER BY col;
SELECT Code, Name, IsDemo FROM Organizations;
SELECT Email, FullName FROM Users;
