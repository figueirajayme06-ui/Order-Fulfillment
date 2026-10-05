CREATE TABLE [dbo].[CPQ_Attribute] (
    [Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [AttributeDescription] NVARCHAR (40)  NOT NULL,
    [CPQAttribute]         NVARCHAR (40)  NOT NULL,
    [DataType]             NVARCHAR (6)   NULL,
    [ValidationLength]     INT            NULL,
    [VerCol]               ROWVERSION     NOT NULL,
    [AttributeName]        NVARCHAR (100) NULL,
    CONSTRAINT [CPQ_Attirbute_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [CPQ_Attribute_UC]
    ON [dbo].[CPQ_Attribute]([CPQAttribute] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [CPQ_AttributeDescription_UC]
    ON [dbo].[CPQ_Attribute]([AttributeDescription] ASC);

