CREATE TABLE [dbo].[DataRefresh](
	[Key] NVARCHAR(200) NOT NULL,
	[Description] [nvarchar](MAX) NOT NULL,
	[LastSuccessfulRunUTC] DATETIME2 (7)  NULL
    CONSTRAINT [PK_DataRefresh] PRIMARY KEY CLUSTERED ([Key] ASC)
);


