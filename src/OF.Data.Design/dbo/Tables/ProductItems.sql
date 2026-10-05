CREATE TABLE [dbo].[ProductItems] (
    [Warehouse]         NVARCHAR (100)  NOT NULL,
    [ItemNumber]        NVARCHAR (100)  NOT NULL,
    [StockQuantity]     DECIMAL (18, 2) NOT NULL,
    [AllocatedQuantity] DECIMAL (18, 2) NOT NULL,
    [DefaultLocation]   NVARCHAR (100)  NULL,
    [Facility]          NVARCHAR (100)  NOT NULL,
    [Division]          NVARCHAR (100)  NOT NULL,
    [Status]            NVARCHAR (100)  NOT NULL,
    [Description]       NVARCHAR (100)  NULL,
    CONSTRAINT [PK_ProductItems] PRIMARY KEY CLUSTERED ([Warehouse] ASC, [ItemNumber] ASC)
);
GO
CREATE NONCLUSTERED INDEX [ItemNumber_Warehouse_Inc] ON [dbo].[ProductItems] ([ItemNumber], [Warehouse]) INCLUDE ([AllocatedQuantity], [DefaultLocation], [Division], [Facility], [Status], [StockQuantity])
GO

