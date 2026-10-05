CREATE TABLE [dbo].[CPQ_Purpose_Staging] (
    [Id]                 INT           IDENTITY (1, 1) NOT NULL,
    [PurposeDescription] NVARCHAR (50) NOT NULL,
    CONSTRAINT [PK_CPQ_Purpose_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

