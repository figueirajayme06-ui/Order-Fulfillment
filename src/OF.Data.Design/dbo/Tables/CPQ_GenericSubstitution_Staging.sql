CREATE TABLE [dbo].[CPQ_GenericSubstitution_Staging] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [ParentGenericId] INT           NOT NULL,
    [ChildGenericId]  INT           NOT NULL,
    [RegionId]        INT           NULL,
    [Purpose]         NVARCHAR (50) NOT NULL,
    [VerCol]          ROWVERSION    NOT NULL,
    CONSTRAINT [PK_CPQ_GenericSubstitution_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

