CREATE TABLE [dbo].[CPQ_RelatedSpecificSubstitution] (
    [Id]           INT        IDENTITY (1, 1) NOT NULL,
    [ParentItemId] INT        NOT NULL,
    [ChildItemId]  INT        NOT NULL,
    [VerCol]       ROWVERSION NOT NULL,
    CONSTRAINT [PK_CPQ_RelatedSpecificSubstitution] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_RelatedSpecificSubstitution_ChildId_FK] FOREIGN KEY ([ChildItemId]) REFERENCES [dbo].[CPQ_Item] ([Id]),
    CONSTRAINT [CPQ_RelatedSpecificSubstitution_ParentId_FK] FOREIGN KEY ([ParentItemId]) REFERENCES [dbo].[CPQ_Item] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_RelatedSpecificSubstitution_ChildItemId]
    ON [dbo].[CPQ_RelatedSpecificSubstitution]([ChildItemId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_RelatedSpecificSubstitution_ParentItemId]
    ON [dbo].[CPQ_RelatedSpecificSubstitution]([ParentItemId] ASC);

