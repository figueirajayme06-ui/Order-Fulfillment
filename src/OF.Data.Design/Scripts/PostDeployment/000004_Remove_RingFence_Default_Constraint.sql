--  Script also used in conjunction with \Scripts\Migrations\0001_Create_CreatedAt-By_On_Ringfences_With_Defaults.sql
--  Keep it up to date!

DECLARE @000004_placeholder nvarchar(100);

IF EXISTS(SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'Ringfences'
        and columns.COLUMN_NAME = N'CreatedBy'
        and columns.COLUMN_DEFAULT IS NOT NULL)
BEGIN
SET @000004_placeholder = (
    SELECT TRIM('(' FROM TRIM(')' FROM TRIM('N' FROM COLUMN_DEFAULT)))
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'Ringfences'
        and columns.COLUMN_NAME = N'CreatedBy');
SET @000004_placeholder = SUBSTRING(@000004_placeholder, 3, LEN(@000004_placeholder) - 3); --  Remove pair of single quotes

ALTER TABLE [dbo].[Ringfences]
DROP [Ringfences_DF_CreatedBy];

UPDATE [dbo].[Ringfences]
SET CreatedBy = COALESCE(Owner, LastUpdatedBy)
WHERE (LastUpdatedBy IS NOT NULL OR Owner IS NOT NULL) AND CreatedBy = @000004_placeholder;

UPDATE [dbo].[Ringfences]
SET CreatedAt = LastUpdatedDate
WHERE LastUpdatedDate IS NOT NULL AND CreatedAt > LastUpdatedDate;
END

IF EXISTS(SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'RingfenceItems'
        and columns.COLUMN_NAME = N'CreatedBy'
        and columns.COLUMN_DEFAULT IS NOT NULL)
BEGIN
SET @000004_placeholder = (
    SELECT TRIM('(' FROM TRIM(')' FROM TRIM('N' FROM COLUMN_DEFAULT)))
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'RingfenceItems'
        and columns.COLUMN_NAME = N'CreatedBy');
SET @000004_placeholder = SUBSTRING(@000004_placeholder, 3, LEN(@000004_placeholder) - 3); --  Remove pair of single quotes

ALTER TABLE [dbo].[RingfenceItems]
DROP [RingfenceItems_DF_CreatedBy]

UPDATE [dbo].[RingfenceItems]
SET CreatedBy = LastUpdatedBy
WHERE LastUpdatedBy IS NOT NULL AND CreatedBy = @000004_placeholder;

UPDATE [dbo].[RingfenceItems]
SET CreatedAt = LastUpdatedDate
WHERE LastUpdatedDate IS NOT NULL AND CreatedAt > LastUpdatedDate;
END