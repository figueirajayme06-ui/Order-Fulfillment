CREATE TABLE [dbo].[Lines] (
    [Id]                   INT             IDENTITY (1, 1) NOT NULL,
    [HeaderId]             INT             NULL,
    [OrderLineNumber]      NVARCHAR (450)  NULL,
    [AgreementLineNumber]  NVARCHAR (450)  NULL,
    [ItemNumber]           NVARCHAR (200)  NULL,
    [DeliveryDate]         DATETIME2 (7)   NULL,
    [ValidToDate]          DATETIME2 (7)   DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [TerminationDate]      DATETIME2 (7)   NULL,
    [Quantity]             REAL            DEFAULT ((0.0000000000000000e+000)) NOT NULL,
    [AgreementLineType]    NVARCHAR (200)  NULL,
    [Warehouse]            NVARCHAR (200)  DEFAULT (N'') NOT NULL,
    [Status]               NVARCHAR (20)   NULL,
    [Division]             NVARCHAR (100)  DEFAULT (N'') NOT NULL,
    [PackageGroupNumber]   NVARCHAR (200)  NULL,
    [Attributes]           NVARCHAR (2000) NULL,
    [LocalizedAttributes]  NVARCHAR (2000) NULL,
    [ValidFromDate]        DATETIME2 (7)   DEFAULT ('0001-01-01T00:00:00.0000000') NOT NULL,
    [ChangeSequence]       BIGINT          DEFAULT (0) NOT NULL,
    [GenericItemNumber]    NVARCHAR (200)  NULL,
    [IsDeleted]            BIT             DEFAULT (0) NOT NULL,
    [FulfilmentStatus]     INT             DEFAULT ((0)) NOT NULL,
    [QuantityFulfilled]    FLOAT (53)      DEFAULT ((0.0000000000000000e+000)) NOT NULL,
    [LastUpdatedBy]        NVARCHAR (MAX)  NULL,
    [LastUpdatedDate]      DATETIME2 (7)   NULL,
    [AgreementLineIndex]   INT             NULL,
    [Facility]             NVARCHAR (100)  DEFAULT (N'') NOT NULL,
    [OrderLineIndex]       INT             NULL,
    [QuoteLineIndex]       INT             NULL,
    [QuoteLineNumber]      NVARCHAR (450)  NULL,
    [OrderSource]          NVARCHAR (100)  DEFAULT (N'') NOT NULL,
    [AgreementNumbersOnly] NVARCHAR (100)  NULL,
    [QuotePublicId] NVARCHAR(100) NULL, 
    [QuotePublicIdNumbersOnly] NVARCHAR(100) NULL, 
    [NumberOfShifts] NVARCHAR (500) NULL, 
    [ActivationErrors] NVARCHAR (MAX) NULL,
    [RateType] NVARCHAR (500) NULL,
    [CollectionDate]         DATETIME2 (7)   NULL,
    [DescriptionWithAttributes] NVARCHAR(MAX) NULL, 
    [ActivationStatus] INT DEFAULT ((0)) NOT NULL,
    [ActivationInstanceId] NVARCHAR(500) NULL, 
    [ItemDescription] NVARCHAR(120) NULL, 
    [RequiresFulfilment] BIT NOT NULL DEFAULT (1), 
    CONSTRAINT [PK_Lines] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Lines_Headers_HeaderId] FOREIGN KEY ([HeaderId]) REFERENCES [dbo].[Headers] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [HeaderId_IsDeleted_RequiresFulfilment_Inc] ON [dbo].[Lines] ([HeaderId], [IsDeleted], [RequiresFulfilment]) INCLUDE ([AgreementLineNumber], [AgreementNumbersOnly], [ItemNumber])
GO
CREATE NONCLUSTERED INDEX [IX_Lines_QuoteLineNumber] ON [dbo].[Lines] ([QuoteLineNumber])
GO
CREATE NONCLUSTERED INDEX [QuotePublicId_Inc] ON [dbo].[Lines] ([QuotePublicId]) INCLUDE ([HeaderId], [OrderLineNumber], [AgreementLineNumber], [ItemNumber], [DeliveryDate], [ValidToDate], [TerminationDate], [Quantity], [AgreementLineType], [Warehouse], [Status], [Division], [PackageGroupNumber], [Attributes], [LocalizedAttributes], [ValidFromDate], [ChangeSequence], [GenericItemNumber], [IsDeleted], [FulfilmentStatus], [QuantityFulfilled], [LastUpdatedBy], [LastUpdatedDate], [AgreementLineIndex], [Facility], [OrderLineIndex], [QuoteLineIndex], [QuoteLineNumber], [OrderSource], [AgreementNumbersOnly], [QuotePublicIdNumbersOnly], [NumberOfShifts], [ActivationErrors], [RateType], [CollectionDate], [DescriptionWithAttributes], [ActivationStatus], [ActivationInstanceId], [ItemDescription], [RequiresFulfilment])
GO




