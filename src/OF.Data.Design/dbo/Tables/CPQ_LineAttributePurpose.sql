CREATE TABLE [dbo].[CPQ_LineAttributePurpose] (
    [Id]          INT          IDENTITY (1, 1) NOT NULL,
    [LineId]      INT          NOT NULL,
    [AttributeId] INT          NOT NULL,
    [PurposeId]   INT          NOT NULL,
    [Active]      NVARCHAR (5) NOT NULL,
    [VerCol]      ROWVERSION   NOT NULL,
    CONSTRAINT [CPQ_LineAttributePurpose_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_LineAttributePurpose_AttributeId_FK] FOREIGN KEY ([AttributeId]) REFERENCES [dbo].[CPQ_Attribute] ([Id]),
    CONSTRAINT [CPQ_LineAttributePurpose_LineId_FK] FOREIGN KEY ([LineId]) REFERENCES [dbo].[CPQ_Line] ([Id]),
    CONSTRAINT [CPQ_LineAttributePurpose_PurposeId_FK] FOREIGN KEY ([PurposeId]) REFERENCES [dbo].[CPQ_Purpose] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_LineAttributePurpose_AttributeId]
    ON [dbo].[CPQ_LineAttributePurpose]([AttributeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_LineAttributePurpose_LineId]
    ON [dbo].[CPQ_LineAttributePurpose]([LineId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_LineAttributePurpose_PurposeId]
    ON [dbo].[CPQ_LineAttributePurpose]([PurposeId] ASC);

