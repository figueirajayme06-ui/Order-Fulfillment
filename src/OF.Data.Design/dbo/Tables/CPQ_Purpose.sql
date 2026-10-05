CREATE TABLE [dbo].[CPQ_Purpose] (
    [Id]                 INT           IDENTITY (1, 1) NOT NULL,
    [PurposeDescription] NVARCHAR (50) NOT NULL,
    CONSTRAINT [CPQ_Purpose_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);

