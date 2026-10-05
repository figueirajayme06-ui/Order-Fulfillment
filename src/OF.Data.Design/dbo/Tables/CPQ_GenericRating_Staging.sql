CREATE TABLE [dbo].[CPQ_GenericRating_Staging] (
    [Id]          INT           IDENTITY (1, 1) NOT NULL,
    [RegionId]    INT           NOT NULL,
    [GenericId]   INT           NOT NULL,
    [GenericCode] NVARCHAR (20) NOT NULL,
    [Rating_Intl] INT           NOT NULL,
    CONSTRAINT [PK_CPQ_GenericRating_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

