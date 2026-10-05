CREATE TABLE [dbo].[CPQ_Attribute_Staging] (
    [Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [AttributeDescription] NVARCHAR (40)  NOT NULL,
    [CPQAttribute]         NVARCHAR (40)  NOT NULL,
    [DataType]             NVARCHAR (6)   NULL,
    [ValidationLength]     INT            NULL,
    [VerCol]               ROWVERSION     NOT NULL,
    [AttributeName]        NVARCHAR (100) NULL,
    CONSTRAINT [PK_CPQ_Attribute_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

