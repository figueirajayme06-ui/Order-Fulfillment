CREATE TABLE [dbo].[ProcessingErrors] (
    [Id]                INT IDENTITY(1,1) NOT NULL,
    [ProcessName]       NVARCHAR(100) NOT NULL,
    [ProcessId]         NVARCHAR(100) NULL,
    [RecordId]          NVARCHAR(100) NULL,
    [RecordType]        NVARCHAR(50) NULL,
    [ErrorMessage]      NVARCHAR(MAX) NOT NULL,
    [ErrorDetails]      NVARCHAR(MAX) NULL,
    [ProcessedAt]       DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy]         NVARCHAR(100) NOT NULL DEFAULT 'System',

    CONSTRAINT [PK_ProcessingErrors] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO
CREATE INDEX [IX_ProcessingErrors_ProcessName] ON [dbo].[ProcessingErrors] ([ProcessName]);
GO
CREATE INDEX [IX_ProcessingErrors_ProcessedAt] ON [dbo].[ProcessingErrors] ([ProcessedAt] DESC);
GO
CREATE INDEX [IX_ProcessingErrors_RecordId] ON [dbo].[ProcessingErrors] ([RecordId]);
GO
CREATE INDEX [IX_ProcessingErrors_ProcessId] ON [dbo].[ProcessingErrors] ([ProcessId]);
GO