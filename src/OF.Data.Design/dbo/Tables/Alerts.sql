CREATE TABLE [dbo].[Alerts] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [LineId]          INT            NOT NULL,
    [Text]            NVARCHAR (MAX) NOT NULL,
    [AffectedUser]    NVARCHAR (200) NOT NULL,
    [Acknowledged]    BIT            NOT NULL,
    [LastUpdatedBy]   NVARCHAR (MAX) NULL,
    [LastUpdatedDate] DATETIME2 (7)  NULL,
    CONSTRAINT [PK_Alerts] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [AffectedUser_Inc] ON [dbo].[Alerts] ([AffectedUser]) INCLUDE ([Acknowledged], [LastUpdatedBy], [LastUpdatedDate], [LineId], [Text])
GO


