CREATE TABLE [dbo].[CPQ_Region] (
    [Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [RegionDescription]  NVARCHAR (50)  NOT NULL,
    [RegionOrgCode]      NVARCHAR (20)  NULL,
    [CPQPriceBook]       NVARCHAR (255) NULL,
    [IsActive]           BIT            NULL,
    [IsStandard]         BIT            NULL,
    [RegionAbbreviation] NVARCHAR (50)  NULL,
    CONSTRAINT [CPQ_Region_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);

