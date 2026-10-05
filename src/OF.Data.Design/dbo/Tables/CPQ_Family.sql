CREATE TABLE [dbo].[CPQ_Family] (
    [Id]                INT           IDENTITY (1, 1) NOT NULL,
    [FamilyDescription] NVARCHAR (50) NOT NULL,
    CONSTRAINT [CPQ_Family_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);

