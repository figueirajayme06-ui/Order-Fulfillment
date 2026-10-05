CREATE TABLE [dbo].[WarehouseItemsStaging] (
    [WarehouseCode] NVARCHAR (100) NOT NULL,
    [Warehouse]     NVARCHAR (100) NOT NULL,
    [DivisionCode]  NVARCHAR (100) NOT NULL,
    [FacilityCode]  NVARCHAR (100) NOT NULL,
    [Facility]      NVARCHAR (100) NOT NULL,
    [CountryCode]   NVARCHAR (100) NOT NULL,
    [Country]       NVARCHAR (200) NOT NULL, 
    CONSTRAINT [PK_WarehouseItemsStaging] PRIMARY KEY ([WarehouseCode])
);

