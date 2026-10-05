CREATE TABLE [dbo].[CPQ_GenericToGeneric] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [ParentGenericId] INT           NOT NULL,
    [ChildGenericId]  INT           NOT NULL,
    [Purpose]         NVARCHAR (50) NOT NULL,
    [Optionality]     NVARCHAR (20) NOT NULL,
    [VerCol]          ROWVERSION    NOT NULL,
    CONSTRAINT [CPQ_GenericToGeneric_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_GenericToGeneric_ChildId_FK] FOREIGN KEY ([ChildGenericId]) REFERENCES [dbo].[CPQ_Generic] ([Id]),
    CONSTRAINT [CPQ_GenericToGeneric_ParentId_FK] FOREIGN KEY ([ParentGenericId]) REFERENCES [dbo].[CPQ_Generic] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_GenericToGeneric_ChildGenericId]
    ON [dbo].[CPQ_GenericToGeneric]([ChildGenericId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_GenericToGeneric_ParentGenericId]
    ON [dbo].[CPQ_GenericToGeneric]([ParentGenericId] ASC);

