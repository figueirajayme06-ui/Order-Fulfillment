CREATE TABLE [dbo].[ChangeOrderLines] (
    [Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [LineId]               INT            NULL,
    [DeliveryDate]         DATETIME2 (7)  NULL,
    [OnHireDate]           DATETIME2 (7)  NULL,
    [OffHireDate]          DATETIME2 (7)  NULL,
    [TerminationDate]      DATETIME2 (7)  NULL,
    [CollectionDate]       DATETIME2 (7)  NULL,
    [Warehouse]            NVARCHAR (200)  DEFAULT (N'') NOT NULL,
    [GenericItemNumber]    NVARCHAR (200)  NULL,
    [ItemNumber]           NVARCHAR (200)  NULL,
    [Attributes]           NVARCHAR (2000) NULL,
    [Quantity]             REAL            DEFAULT ((0.0000000000000000e+000)) NULL,
    [Price]                DECIMAL (18,2) NULL,
    [Status]               INT NOT NULL DEFAULT (0),
    [CorrelationId] NVARCHAR(500) NULL, 
    [ChangeOrderId] INT NOT NULL, 
    [Delete] BIT NOT NULL DEFAULT (0), 
    CONSTRAINT [PK_ChangeOrderLines] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChangeOrderLines_ChangeOrderLines_LineId] FOREIGN KEY ([LineId]) REFERENCES [dbo].[Lines] ([Id]),
    CONSTRAINT [FK_ChangeOrderLines_ChangeOrders_ChangeOrderId] FOREIGN KEY ([ChangeOrderId]) REFERENCES [dbo].[ChangeOrders] ([Id])
);




 