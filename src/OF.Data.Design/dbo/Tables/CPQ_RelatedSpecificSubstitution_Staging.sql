CREATE TABLE [dbo].[CPQ_RelatedSpecificSubstitution_Staging] (
    [Id]           INT        IDENTITY (1, 1) NOT NULL,
    [ParentItemId] INT        NOT NULL,
    [ChildItemId]  INT        NOT NULL,
    [VerCol]       ROWVERSION NOT NULL,
    CONSTRAINT [PK_CPQ_RelatedSpecificSubstitution_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

