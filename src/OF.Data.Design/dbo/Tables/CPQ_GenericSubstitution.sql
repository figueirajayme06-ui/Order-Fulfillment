CREATE TABLE [dbo].[CPQ_GenericSubstitution] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [ParentGenericId] INT           NOT NULL,
    [ChildGenericId]  INT           NOT NULL,
    [RegionId]        INT           NULL,
    [Purpose]         NVARCHAR (50) NOT NULL,
    [VerCol]          ROWVERSION    NOT NULL,
    CONSTRAINT [CPQ_GenericSubstitution_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_GenericSubstitution_ChildId_FK] FOREIGN KEY ([ChildGenericId]) REFERENCES [dbo].[CPQ_Generic] ([Id]),
    CONSTRAINT [CPQ_GenericSubstitution_ParentId_FK] FOREIGN KEY ([ParentGenericId]) REFERENCES [dbo].[CPQ_Generic] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_GenericSubstitution_ChildGenericId]
    ON [dbo].[CPQ_GenericSubstitution]([ChildGenericId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_GenericSubstitution_ParentGenericId]
    ON [dbo].[CPQ_GenericSubstitution]([ParentGenericId] ASC);

