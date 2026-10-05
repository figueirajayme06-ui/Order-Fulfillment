CREATE TABLE [dbo].[CPQ_RegionGeneric_Staging] (
    [Id]        INT        IDENTITY (1, 1) NOT NULL,
    [RegionId]  INT        NOT NULL,
    [GenericId] INT        NOT NULL,
    [VerCol]    ROWVERSION NOT NULL,
    CONSTRAINT [PK_CPQ_RegionGeneric_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

