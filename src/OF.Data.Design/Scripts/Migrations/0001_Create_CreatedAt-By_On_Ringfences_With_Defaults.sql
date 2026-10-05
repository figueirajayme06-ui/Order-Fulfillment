
IF NOT EXISTS(
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'Ringfences'
        and columns.COLUMN_NAME = N'CreatedAt')
BEGIN
ALTER TABLE [dbo].[Ringfences]
ADD CreatedAt DATETIMEOFFSET(7) CONSTRAINT [Ringfences_DF_CreatedAt] DEFAULT (SYSDATETIMEOFFSET()) WITH VALUES;
END

IF NOT EXISTS(
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'Ringfences'
        and columns.COLUMN_NAME = N'CreatedBy')
BEGIN
ALTER TABLE [dbo].[Ringfences]
ADD CreatedBy NVARCHAR(100) CONSTRAINT [Ringfences_DF_CreatedBy] DEFAULT N'admin.migration@aggreko.com' WITH VALUES;
END

IF NOT EXISTS(
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'RingfenceItems'
        and columns.COLUMN_NAME = N'CreatedAt')
BEGIN
ALTER TABLE [dbo].[RingfenceItems]
ADD CreatedAt DATETIMEOFFSET(7) CONSTRAINT [RingfenceItems_DF_CreatedAt] DEFAULT (SYSDATETIMEOFFSET()) WITH VALUES;
END

IF NOT EXISTS(
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS columns
    WHERE columns.TABLE_SCHEMA = N'dbo'
        and columns.TABLE_NAME = N'RingfenceItems'
        and columns.COLUMN_NAME = N'CreatedBy')
BEGIN
ALTER TABLE [dbo].[RingfenceItems]
ADD CreatedBy NVARCHAR(100) CONSTRAINT [RingfenceItems_DF_CreatedBy] DEFAULT N'admin.migration@aggreko.com' WITH VALUES;
END

-- Then execute ~\Scripts\PostDeployment\000004_Remove_RingFence_Default_Constraint.sql