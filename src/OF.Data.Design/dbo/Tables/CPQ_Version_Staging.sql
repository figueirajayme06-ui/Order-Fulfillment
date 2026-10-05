CREATE TABLE [dbo].[CPQ_Version_Staging] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [SpreadsheetName] NVARCHAR (MAX) NOT NULL,
    [Application]     NVARCHAR (MAX) NOT NULL,
    [VersionNumber]   NVARCHAR (10)  NOT NULL,
    [DataAdded]       DATETIME       NOT NULL,
    [IsLatest]        BIT            NOT NULL,
    [UploadedBy]      NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_CPQ_Version_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

