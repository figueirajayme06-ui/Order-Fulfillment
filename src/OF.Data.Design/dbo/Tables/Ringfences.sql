CREATE TABLE [dbo].[Ringfences] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [FromDate]        DATETIME2 (7)  NOT NULL,
    [ToDate]          DATETIME2 (7)  NOT NULL,
    [Title]           NVARCHAR (MAX) NOT NULL,
    [LastUpdatedBy]   NVARCHAR (MAX) NULL,
    [LastUpdatedDate] DATETIME2 (7)  NULL,
    [Divisions]       NVARCHAR (MAX) DEFAULT (N'') NOT NULL,
    [Owner]           NVARCHAR(MAX) NULL, 
    [Warehouse]       NVARCHAR(100) NULL, 
    [CreatedAt]       DATETIMEOFFSET(7) NOT NULL CONSTRAINT [Ringfences_DF_CreatedAt] DEFAULT (SYSDATETIMEOFFSET()),
    [CreatedBy]       NVARCHAR(100) NOT NULL CONSTRAINT [Ringfences_DF_CreatedBy] DEFAULT (N'admin.migration@aggreko.com'), -- Default constraint only for migration, removed by post deployment script
    CONSTRAINT [PK_Ringfences] PRIMARY KEY CLUSTERED ([Id] ASC)
);

