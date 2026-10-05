CREATE TABLE [dbo].[CPQ_LineAttributePurpose_Staging] (
    [Id]          INT          IDENTITY (1, 1) NOT NULL,
    [LineId]      INT          NOT NULL,
    [AttributeId] INT          NOT NULL,
    [PurposeId]   INT          NOT NULL,
    [Active]      NVARCHAR (5) NOT NULL,
    [VerCol]      ROWVERSION   NOT NULL,
    CONSTRAINT [PK_CPQ_LineAttributePurpose_Staging] PRIMARY KEY CLUSTERED ([Id] ASC)
);

