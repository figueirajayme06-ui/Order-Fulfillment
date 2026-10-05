CREATE TABLE [dbo].[RingfenceItems] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [RingfenceId]     INT            NULL,
    [AssetId]         NVARCHAR (MAX) NOT NULL,
    [LastUpdatedBy]   NVARCHAR (MAX) NULL,
    [LastUpdatedDate] DATETIME2 (7)  NULL,
    [CreatedAt]       DATETIMEOFFSET(7) NOT NULL CONSTRAINT [RingfenceItems_DF_CreatedAt] DEFAULT (SYSDATETIMEOFFSET()),
    [CreatedBy]       NVARCHAR(100) NOT NULL CONSTRAINT [RingfenceItems_DF_CreatedBy] DEFAULT (N'admin.migration@aggreko.com'), -- Default constraint only for migration, removed by post deployment script
    CONSTRAINT [PK_RingfenceItems] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
CREATE NONCLUSTERED INDEX [RingfenceId_Inc] ON [dbo].[RingfenceItems] ([RingfenceId]) INCLUDE ([AssetId])
GO


