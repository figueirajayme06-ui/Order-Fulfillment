CREATE TABLE [dbo].[ProductItems_Staging] (
    [Warehouse]         NVARCHAR (100)  NOT NULL,
    [ItemNumber]        NVARCHAR (100)  NOT NULL,
    [StockQuantity]     DECIMAL (18, 2) NOT NULL,
    [AllocatedQuantity] DECIMAL (18, 2) NOT NULL,
    [DefaultLocation]   NVARCHAR (100)  NULL,
    [Facility]          NVARCHAR (100)  NOT NULL,
    [Division]          NVARCHAR (100)  NOT NULL,
    [Status]            NVARCHAR (100)  NOT NULL,
    [Description]       NVARCHAR (100)  NULL,
    CONSTRAINT [PK_ProductItems_Staging] PRIMARY KEY CLUSTERED ([Warehouse] ASC, [ItemNumber] ASC)
);
GO
CREATE NONCLUSTERED INDEX [ItemNumber_Warehouse_Inc] ON [dbo].[ProductItems_Staging] ([ItemNumber], [Warehouse]) INCLUDE ([AllocatedQuantity], [DefaultLocation], [Division], [Facility], [Status], [StockQuantity])
GO


