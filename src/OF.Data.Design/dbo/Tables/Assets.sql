CREATE TABLE [dbo].[Assets] (
    [Id]                       NVARCHAR (100) NOT NULL,
    [IndividualItemNumber]     NVARCHAR (100) NOT NULL,
    [StatusCode]               NVARCHAR (100) NULL,
    [Warehouse]                NVARCHAR (100) NULL,
    [ShipAddress1]             NVARCHAR (100) NULL,
    [AgreementNumber]          NVARCHAR (100) NULL,
    [DeliveryDate]             DATETIME2 (7)  NULL,
    [AgreementLineValidToDate] DATETIME2 (7)  NULL,
    [TerminationDate]          DATETIME2 (7)  NULL,
    [CustomerName]             NVARCHAR (100) NULL,
    [TelemetryStatus]          NVARCHAR (100) NULL,
    [ServiceCenter]            NVARCHAR (100) NULL,
    [Description]              NVARCHAR (100) NULL,
    [Status]                   NVARCHAR (100) NULL,
    [ManufacturerName]         NVARCHAR (100) NULL,
    [OwnerServiceCenter]       NVARCHAR (100) NULL,
    [CustomerNumber]           NVARCHAR (100) NULL,
    [ItemNumber]               NVARCHAR (100) NULL,
    [ShipAddress3]             NVARCHAR (100) NULL,
    [Facility]                 NVARCHAR (100) NULL,
    [IONLastModified]          DATETIME2  NULL,
    [WarehouseLocation]        NVARCHAR (100) NULL,
    [Division]                 NVARCHAR (100) NULL, 
    [Remark] NVARCHAR(100) NULL, 
    [ProductGroup] NVARCHAR(100) NULL, 
    [ProductCategory] NVARCHAR(100) NULL, 
    [InternationalSizeRating] NVARCHAR(100) NULL, 
    [UsSizeRating] NVARCHAR(100) NULL, 
    [RunHours] FLOAT NULL, 
    [EstimatedReadyDate] DATETIME2 NULL,
    [AgreementLineValidFromDate] DATETIME2 (7)  NULL,
    [CollectionDate] DATETIME2 (7)  NULL, 
    CONSTRAINT [PK_Assets] PRIMARY KEY ([Id])
);
GO

CREATE NONCLUSTERED INDEX [Division_Status_Incl] ON [dbo].[Assets] ([Division], [Status]) INCLUDE ([AgreementLineValidFromDate], [AgreementLineValidToDate], [AgreementNumber], [CollectionDate], [CustomerName], [CustomerNumber], [DeliveryDate], [Description], [EstimatedReadyDate], [Facility], [IONLastModified], [ItemNumber], [ProductCategory], [ProductGroup], [Remark], [RunHours], [TelemetryStatus], [TerminationDate], [UsSizeRating], [Warehouse], [WarehouseLocation])
GO
CREATE NONCLUSTERED INDEX [ItemNumber_Inc] ON [dbo].[Assets] ([ItemNumber]) INCLUDE ([AgreementLineValidToDate], [AgreementNumber], [CustomerName], [CustomerNumber], [DeliveryDate], [Description], [Division], [EstimatedReadyDate], [Facility], [IndividualItemNumber], [IONLastModified], [ManufacturerName], [OwnerServiceCenter], [ServiceCenter], [ShipAddress1], [ShipAddress3], [Status], [StatusCode], [TelemetryStatus], [TerminationDate], [Warehouse], [WarehouseLocation])
GO
CREATE NONCLUSTERED INDEX [Status_Inc] ON [dbo].[Assets] ([Status]) INCLUDE ([AgreementLineValidFromDate], [AgreementNumber], [CollectionDate], [CustomerName], [DeliveryDate], [Id])
GO


