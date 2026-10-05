CREATE TABLE [dbo].[Views] (
    [Id]           INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (100) NOT NULL,
    [ForEveryone]  INT            NOT NULL,
    [Owner]        NVARCHAR (100) NOT NULL,
    [ViewJson]     NVARCHAR (MAX) NOT NULL,
    [ForDivisions] NVARCHAR (MAX) NULL,
    [GanttView]    BIT            DEFAULT (CONVERT([bit],(0))) NOT NULL,
    [AssetView]    BIT            DEFAULT (CONVERT([bit],(0))) NOT NULL,
    CONSTRAINT [PK_Views] PRIMARY KEY CLUSTERED ([Id] ASC)
);

