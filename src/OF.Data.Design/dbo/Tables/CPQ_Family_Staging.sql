CREATE TABLE [dbo].[CPQ_Family_Staging] (
    [Id]                INT           IDENTITY (1, 1) NOT NULL,
    [FamilyDescription] NVARCHAR (50) NOT NULL,
    CONSTRAINT [PK_CPQ_Family_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

