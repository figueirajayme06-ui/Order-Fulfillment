CREATE TABLE [dbo].[CPQ_RegionCurrency_Staging] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [RegionId]        INT           NOT NULL,
    [CurrencyIsoCode] NVARCHAR (50) NOT NULL,
    [IsActive]        BIT           NOT NULL,
    CONSTRAINT [PK_CPQ_RegionCurrency_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

