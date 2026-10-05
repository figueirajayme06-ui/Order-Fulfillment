CREATE TABLE [dbo].[CPQ_Item_Staging] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [DescriptionNam]  NVARCHAR (100) NOT NULL,
    [DescriptionIntl] NVARCHAR (100) NOT NULL,
    [ItemNumber]      NVARCHAR (100) NOT NULL,
    [GenericId]       INT            NOT NULL,
    [Active]          BIT            NULL,
    [Deleted]         BIT            NOT NULL,
    [CPQ_Sequence]    INT            NULL,
    [VerCol]          ROWVERSION     NOT NULL,
    CONSTRAINT [PK_CPQ_Item_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

