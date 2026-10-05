CREATE TABLE [dbo].[ProductHierarchy_Staging] (
    [IndividualItemNumber]         NVARCHAR (100) NOT NULL,
    [ProductGroup]        NVARCHAR (100) NULL,
    [ProductCategory]     NVARCHAR (100) NULL,
    [InternationalActualRatingKey] NVARCHAR (100) NULL,
    [UsActualRatingKey]   NVARCHAR (100) NULL
    CONSTRAINT [PK_ProductHierarchy_Staging] PRIMARY KEY CLUSTERED ([IndividualItemNumber] ASC)
);

