CREATE TABLE [dbo].[CPQ_Log] (
    [Id]       INT            IDENTITY (1, 1) NOT NULL,
    [RunOn]    DATETIME       NOT NULL,
    [IsError]  BIT            NOT NULL DEFAULT 0,
    [RunType]  NVARCHAR (50)  NOT NULL,
    [ItemType] NVARCHAR (50)  NOT NULL,
    [ItemJson] NVARCHAR (MAX) NULL,
    [Reason]   NVARCHAR (MAX) NULL,
    CONSTRAINT [CPQ_Log_PK] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_Log_RunOn]
    ON [dbo].[CPQ_Log]([RunOn] ASC);

