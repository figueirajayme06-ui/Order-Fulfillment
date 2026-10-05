CREATE TABLE [dbo].[ChangeOrderComments] (
    [Id]                   INT            IDENTITY (1, 1) NOT NULL,
    [ChangeOrderId]             INT NOT NULL,
    [Comment]               NVARCHAR(MAX) NOT NULL ,
    [CreatedBy]            NVARCHAR (MAX)  NOT NULL,
    [CreatedDate]          DATETIME2 (7)  NOT NULL,
    CONSTRAINT [PK_ChangeOrderComments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChangeOrderComments_ChangeOrders_ChangeOrderId] FOREIGN KEY ([ChangeOrderId]) REFERENCES [dbo].[ChangeOrders] ([Id]), 
);
