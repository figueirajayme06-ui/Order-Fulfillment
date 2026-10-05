CREATE TABLE [dbo].[CPQ_RegionGeneric] (
    [Id]        INT        IDENTITY (1, 1) NOT NULL,
    [RegionId]  INT        NOT NULL,
    [GenericId] INT        NOT NULL,
    [VerCol]    ROWVERSION NOT NULL,
    CONSTRAINT [CPQ_RegionGeneric_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_RegionGeneric_GenericId_FK] FOREIGN KEY ([GenericId]) REFERENCES [dbo].[CPQ_Generic] ([Id]),
    CONSTRAINT [CPQ_RegionGeneric_RegionId_FK] FOREIGN KEY ([RegionId]) REFERENCES [dbo].[CPQ_Region] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_RegionGeneric_GenericId]
    ON [dbo].[CPQ_RegionGeneric]([GenericId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_RegionGeneric_RegionId]
    ON [dbo].[CPQ_RegionGeneric]([RegionId] ASC);

