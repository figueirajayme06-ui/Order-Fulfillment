CREATE TABLE [dbo].[Notes]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [ParentId] NVARCHAR(20) NOT NULL, 
    [NoteType] NVARCHAR(20) NOT NULL,
    [Note] NVARCHAR(MAX) NOT NULL,
    [LastUpdatedBy]     NVARCHAR (MAX) NOT NULL,
    [LastUpdatedDate]   DATETIME2 (7)  NOT NULL
)

GO

CREATE INDEX [IX_Notes_Column] ON [dbo].[Notes] ([ParentId],[NoteType])
