IF NOT EXISTS(
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'Lines'
        and columns.COLUMN_NAME = N'LocalizedAttributes')
BEGIN
    ALTER TABLE [dbo].[Lines]
    ADD [LocalizedAttributes] NVARCHAR(2000) NULL;
END
