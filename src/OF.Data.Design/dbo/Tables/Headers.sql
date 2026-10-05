CREATE TABLE [dbo].[Headers] (
    [Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [QuotePublicId]        NVARCHAR (100) NULL,
    [AgreementNumber]      NVARCHAR (100) NULL,
    [OnHireDate]           DATETIME2 (7)  NULL,
    [OffHireDate]          DATETIME2 (7)  NULL,
    [Status]               NVARCHAR (100) NULL,
    [CustomerName]         NVARCHAR (500) NULL,
    [CustomerAddress]      NVARCHAR (500) NULL,
    [CustomerNumber]       NVARCHAR (100) NULL,
    [Division]             NVARCHAR (100) DEFAULT (N'') NOT NULL,
    [CustomerAddressCode]  NVARCHAR (100) NULL,
    [OrderSource]          NVARCHAR (100) DEFAULT (N'') NOT NULL,
    [ChangeSequence]       BIGINT         DEFAULT (CONVERT([bigint],(0))) NOT NULL,
    [IsDeleted]            BIT            DEFAULT (CONVERT([bit],(0))) NOT NULL,
    [FulfilmentStatus]     INT            DEFAULT ((0)) NOT NULL,
    [LastUpdatedBy]        NVARCHAR (MAX) NULL,
    [LastUpdatedDate]      DATETIME2 (7)  NULL,
    [Facility]             NVARCHAR (100) DEFAULT (N'') NOT NULL,
    [OpportunityNumber]    NVARCHAR (100) NULL,
    [OrderNumber]          NVARCHAR (100) NULL,
    [QuoteNumber]          NVARCHAR (100) NULL,
    [AgreementNumbersOnly] NVARCHAR (100) NULL,
    [QuotePublicIdNumbersOnly] NVARCHAR(100) NULL,
    [OverviewOfService] NVARCHAR(MAX) NULL,
    [Probability] FLOAT NULL ,
    [ARMContactName] NVARCHAR(500) NULL,
    [ARMContactEmail] NVARCHAR(500) NULL,
    [ARMContactPhone] NVARCHAR(500) NULL,
    [ActivationStatus] INT DEFAULT ((0)) NOT NULL,
    [ActivationErrors] NVARCHAR (MAX) NULL,
    [ActivationInstanceId] NVARCHAR(500) NULL,
    [RentalDepot] NVARCHAR(10) NULL,
    [OpportunityName] NVARCHAR (500) NULL,
    [OpportunityStage] NVARCHAR (100) NULL,
    [IsSkeleton] BIT NULL DEFAULT (0), 
    CONSTRAINT [PK_Headers] PRIMARY KEY CLUSTERED ([Id] ASC)
);

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Headers_AgreementNumber] ON [dbo].[Headers] ([AgreementNumber])
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Headers_AgreementNumbersOnly] ON [dbo].[Headers] ([AgreementNumbersOnly]) WHERE ([AgreementNumbersOnly] IS NOT NULL)
GO
CREATE NONCLUSTERED INDEX [IsDeleted_Inc] ON [dbo].[Headers] ([IsDeleted]) INCLUDE ([Division])
GO
CREATE NONCLUSTERED INDEX [OpportunityNumber] ON [dbo].[Headers] ([OpportunityNumber])
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Headers_OrderNumber] ON [dbo].[Headers] ([OrderNumber]) WHERE ([OrderNumber] IS NOT NULL)
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Headers_QuoteNumber] ON [dbo].[Headers] ([QuoteNumber]) WHERE ([QuoteNumber] IS NOT NULL)



