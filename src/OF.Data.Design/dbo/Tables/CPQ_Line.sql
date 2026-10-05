CREATE TABLE [dbo].[CPQ_Line] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [FamilyId]        INT           NOT NULL,
    [LineDescription] NVARCHAR (50) NULL,
    CONSTRAINT [CPQ_Line_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_Family_Id_FK] FOREIGN KEY ([FamilyId]) REFERENCES [dbo].[CPQ_Family] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_Line_FamilyId]
    ON [dbo].[CPQ_Line]([FamilyId] ASC);

