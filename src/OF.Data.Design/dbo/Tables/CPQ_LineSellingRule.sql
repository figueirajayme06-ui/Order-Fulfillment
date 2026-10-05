CREATE TABLE [dbo].[CPQ_LineSellingRule] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [LineId]          INT           NOT NULL,
    [NAM_Intl]        NVARCHAR (4)  NOT NULL,
    [UOM]             NVARCHAR (50) NOT NULL,
    [MinValue]        NUMERIC (18, 2)  NULL,
    [MaxValue]        NUMERIC (18, 2)  NULL,
    [CPQ_RatingGroup] NVARCHAR (25) NULL,
    CONSTRAINT [CPQ_LineSellingRules_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_LineSellingRules_LineId_FK] FOREIGN KEY ([LineId]) REFERENCES [dbo].[CPQ_Line] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_LineSellingRule_LineId]
    ON [dbo].[CPQ_LineSellingRule]([LineId] ASC);

