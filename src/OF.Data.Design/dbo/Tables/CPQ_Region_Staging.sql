CREATE TABLE [dbo].[CPQ_Region_Staging] (
    [Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [RegionDescription]  NVARCHAR (50)  NOT NULL,
    [RegionOrgCode]      NVARCHAR (20)  NULL,
    [CPQPriceBook]       NVARCHAR (255) NULL,
    [IsActive]           BIT            NULL,
    [IsStandard]         BIT            NULL,
    [RegionAbbreviation] NVARCHAR (50)  NULL,
    CONSTRAINT [PK_CPQ_Region_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

