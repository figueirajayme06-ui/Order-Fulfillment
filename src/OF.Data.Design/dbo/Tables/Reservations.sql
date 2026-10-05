CREATE TABLE [dbo].[Reservations] (
    [Id]                INT            IDENTITY (1, 1) NOT NULL,
    [AssetId]           NVARCHAR (100) NOT NULL,
    [Notes]             NVARCHAR (MAX) NULL,
    [ItemNumber]        NVARCHAR (100) DEFAULT (N'') NOT NULL,
    [Quantity]          INT            DEFAULT ((0)) NOT NULL,
    [Warehouse]         NVARCHAR (100) DEFAULT (N'') NOT NULL,
    [LineId]            INT            DEFAULT ((0)) NOT NULL,
    [LastUpdatedBy]     NVARCHAR (MAX) NULL,
    [LastUpdatedDate]   DATETIME2 (7)  NULL,
    [IsDepotFulfilled]  BIT            DEFAULT (CONVERT([bit],(0))) NOT NULL,
    [IsRehire]          BIT            DEFAULT (CONVERT([bit],(0))) NOT NULL,
    [EffectiveQuantity] FLOAT (53)     NOT NULL,
    [IsConfirmed] BIT NOT NULL DEFAULT ((0)), 
    [ActualAssetId] NVARCHAR(100) NULL, 
    [ActualItemNumber] NVARCHAR(100) NULL, 
    [ActualQuantity] FLOAT NULL, 
    CONSTRAINT [PK_Reservations] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
CREATE NONCLUSTERED INDEX [IsConfirmed_AssetId_Inc] ON [dbo].[Reservations] ([IsConfirmed], [AssetId]) INCLUDE ([LineId], [Notes])
GO
CREATE NONCLUSTERED INDEX [ItemNumber_Warehouse_Inc] ON [dbo].[Reservations] ([ItemNumber], [Warehouse]) INCLUDE ([IsConfirmed], [LineId], [Notes], [Quantity])
GO
CREATE NONCLUSTERED INDEX [LineId_IsConfirmed_IsDepotFulfilled_Inc] ON [dbo].[Reservations] ([LineId], [IsConfirmed], [IsDepotFulfilled]) INCLUDE ([AssetId], [ItemNumber], [Warehouse])
GO


