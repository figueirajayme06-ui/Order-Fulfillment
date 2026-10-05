CREATE TABLE [dbo].[CPQ_LineSellingRule_Staging] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [LineId]          INT           NOT NULL,
    [NAM_Intl]        NVARCHAR (4)  NOT NULL,
    [UOM]             NVARCHAR (50) NOT NULL,
    [MinValue]        NUMERIC (18)  NULL,
    [MaxValue]        NUMERIC (18)  NULL,
    [CPQ_RatingGroup] NVARCHAR (25) NULL,
    CONSTRAINT [PK_CPQ_LineSellingRule_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

