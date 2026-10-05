CREATE TABLE [dbo].[CPQ_RegionCurrency] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [RegionId]        INT           NOT NULL,
    [CurrencyIsoCode] NVARCHAR (50) NOT NULL,
    [IsActive]        BIT           NOT NULL,
    CONSTRAINT [PK_CPQ_Currency] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CPQ_RegionCurrency_CPQ_Region] FOREIGN KEY ([RegionId]) REFERENCES [dbo].[CPQ_Region] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_RegionCurrency_RegionId]
    ON [dbo].[CPQ_RegionCurrency]([RegionId] ASC);

