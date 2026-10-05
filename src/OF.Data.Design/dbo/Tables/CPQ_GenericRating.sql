CREATE TABLE [dbo].[CPQ_GenericRating] (
    [Id]          INT           IDENTITY (1, 1) NOT NULL,
    [RegionId]    INT           NOT NULL,
    [GenericId]   INT           NOT NULL,
    [GenericCode] NVARCHAR (20) NOT NULL,
    [Rating_Intl] INT           NOT NULL,
    CONSTRAINT [CPQ_GenericRating_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);

