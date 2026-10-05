IF COL_LENGTH(N'dbo.Users', N'LastLoginAtUtc') IS NULL
BEGIN
    ALTER TABLE [dbo].[Users]
        ADD [LastLoginAtUtc] DATETIME2 (7) NULL;
END;
