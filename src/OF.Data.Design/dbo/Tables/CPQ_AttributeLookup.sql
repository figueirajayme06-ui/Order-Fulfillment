CREATE TABLE [dbo].[CPQ_AttributeLookup] (
    [Id]          INT            IDENTITY (1, 1) NOT NULL,
    [AttributeId] INT            NOT NULL,
    [LineId]      INT            NOT NULL,
    [Value]       NVARCHAR (100) NOT NULL,
    CONSTRAINT [CPQ_AttributeLookup_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);

