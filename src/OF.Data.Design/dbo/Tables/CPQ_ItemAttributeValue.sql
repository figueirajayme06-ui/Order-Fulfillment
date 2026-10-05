CREATE TABLE [dbo].[CPQ_ItemAttributeValue] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]      INT            NOT NULL,
    [AttributeId] INT            NOT NULL,
    [Value]       NVARCHAR (MAX) NOT NULL,
    [VerCol]      ROWVERSION     NOT NULL,
    CONSTRAINT [CPQ_ItemAttirbuteValue_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_ItemAttributeValue_AttributeId_FK] FOREIGN KEY ([AttributeId]) REFERENCES [dbo].[CPQ_Attribute] ([Id]),
    CONSTRAINT [CPQ_ItemAttributeValue_ItemId_FK] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[CPQ_Item] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_ItemAttributeValue_AttributeId]
    ON [dbo].[CPQ_ItemAttributeValue]([AttributeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_ItemAttributeValue_ItemId_AttributeId]
    ON [dbo].[CPQ_ItemAttributeValue]([ItemId] ASC, [AttributeId] ASC);

