CREATE TABLE [dbo].[CPQ_Line_Staging] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [FamilyId]        INT           NOT NULL,
    [LineDescription] NVARCHAR (50) NULL,
    CONSTRAINT [PK_CPQ_Line_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

