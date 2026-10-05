CREATE TABLE [dbo].[AttributeLanguage] (
    [LookupKey]     NVARCHAR (500) NOT NULL,
    [Name]          NVARCHAR (500) NOT NULL,
    [Language]      NVARCHAR (10) NOT NULL,
    [Translation]   NVARCHAR (500) NOT NULL
);

GO
CREATE NONCLUSTERED INDEX [IX_AttributeLanguage_Translation]
    ON [dbo].[AttributeLanguage]([Translation] ASC);
