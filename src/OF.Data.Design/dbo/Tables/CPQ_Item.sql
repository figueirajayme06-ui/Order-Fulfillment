CREATE TABLE [dbo].[CPQ_Item] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [DescriptionNam]  NVARCHAR (100) NOT NULL,
    [DescriptionIntl] NVARCHAR (100) NOT NULL,
    [ItemNumber]      NVARCHAR (100) NOT NULL,
    [GenericId]       INT            NOT NULL,
    [Active]          BIT            NULL,
    [Deleted]         BIT            NOT NULL,
    [CPQ_Sequence]    INT            NULL,
    [Release]         NVARCHAR (100) NULL,
    [VerCol]          ROWVERSION     NOT NULL,
    CONSTRAINT [CPQ_Item_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_Item_GenericId_FK] FOREIGN KEY ([GenericId]) REFERENCES [dbo].[CPQ_Generic] ([Id])
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [CPQ_Item_UC]
    ON [dbo].[CPQ_Item]([ItemNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_Item_GenericId]
    ON [dbo].[CPQ_Item]([GenericId] ASC);

