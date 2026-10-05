CREATE TABLE [dbo].[ChangeOrderAddresses] (
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [AddressType] INT NOT NULL, -- Shipping, Billing
    [AddressName] NVARCHAR(1000) NULL,
    [Street] NVARCHAR(200) NULL,
    [City] NVARCHAR(200) NULL,
    [StateOrProvince] NVARCHAR(200) NULL,
    [ZipOrPostalCode] NVARCHAR(200) NULL,
    [Country] NVARCHAR(200) NULL,
    [M3Number] NVARCHAR(200) NULL,
    [SalesforceId] NVARCHAR(200) NULL,
    CONSTRAINT [PK_ChangeOrderAddresses] PRIMARY KEY CLUSTERED ([Id] ASC)
);