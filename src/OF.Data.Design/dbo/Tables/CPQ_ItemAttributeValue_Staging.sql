CREATE TABLE [dbo].[CPQ_ItemAttributeValue_Staging] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]      INT            NOT NULL,
    [AttributeId] INT            NOT NULL,
    [Value]       NVARCHAR (800) NOT NULL,
    [VerCol]      ROWVERSION     NOT NULL,
    CONSTRAINT [PK_CPQ_ItemAttributeValue_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

