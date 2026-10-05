CREATE TABLE [dbo].[CPQ_AttributeLookup_Staging] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [AttributeId] INT            NOT NULL,
    [LineId]      INT            NOT NULL,
    [Value]       NVARCHAR (100) NOT NULL,
    CONSTRAINT [PK_CPQ_AttributeLookup_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

