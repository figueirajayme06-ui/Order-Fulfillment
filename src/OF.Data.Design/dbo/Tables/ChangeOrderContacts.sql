CREATE TABLE [dbo].[ChangeOrderContacts] (
    [Id] INT IDENTITY (1, 1) NOT NULL,
    [ContactType] INT NOT NULL, -- Primary Contact, Billing Contact, ARM Contact, Site Contact
    [Title] NVARCHAR(200) NOT NULL,
    [FirstName] NVARCHAR(200) NOT NULL,
    [LastName] NVARCHAR(200) NOT NULL,
    [Phone] NVARCHAR(200) NULL,
    [Mobile] NVARCHAR(200) NULL,
    [Email] NVARCHAR(200) NOT NULL,
    [Segment] NVARCHAR(200) NULL,
    [M3Number] NVARCHAR(200) NULL,
    [SalesforceId] NVARCHAR(200) NULL,
    CONSTRAINT [PK_ChangeOrderContacts] PRIMARY KEY CLUSTERED ([Id] ASC)
);