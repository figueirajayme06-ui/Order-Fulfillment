CREATE TABLE [dbo].[CPQ_GenericToGeneric_Staging] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [ParentGenericId] INT           NOT NULL,
    [ChildGenericId]  INT           NOT NULL,
    [Purpose]         NVARCHAR (50) NOT NULL,
    [Optionality]     NVARCHAR (20) NOT NULL,
    [VerCol]          ROWVERSION    NOT NULL,
    CONSTRAINT [PK_CPQ_GenericToGeneric_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

