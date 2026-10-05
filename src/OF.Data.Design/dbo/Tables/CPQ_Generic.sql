CREATE TABLE [dbo].[CPQ_Generic] (
    [Id]                 INT            IDENTITY (1, 1) NOT NULL,
    [LineId]             INT            NOT NULL,
    [GenericCode]        NVARCHAR (20)  NOT NULL,
    [GenericDescription] NVARCHAR (255) NULL,
    [Active]             BIT            NULL,
    [Deleted]            BIT            NOT NULL,
    [Rental_Term_Days]   INT            NULL,
    [UOM_Intl]           NVARCHAR (50)  NULL,
    [Rating_Intl]        NVARCHAR (50)  NULL,
    [UOM_US]             NVARCHAR (50)  NULL,
    [Rating_US]          NVARCHAR (50)  NULL,
    [Rehire]             NVARCHAR (3)   NULL,
    [Cable_Size_AWG]     NVARCHAR (10)  NULL,
    [Cable_Size_mm]      NVARCHAR (10)  NULL,
    [Amperage_Limit]     NVARCHAR (100) NULL,
    [Conductors]         NVARCHAR (100) NULL,
    [IsFuel]             NVARCHAR (10)  NULL,
    [IsMeter]            NVARCHAR (10)  NULL,
    [M3_Type]            NVARCHAR (15)  NULL,
    [Cable_M3ItemNumber] NVARCHAR (100) NULL,
    [CPQ_Sequence]       INT            NULL,
    [Release]            NVARCHAR (100) NULL,
    [VerCol]             ROWVERSION     NOT NULL,
    [CableType]          NVARCHAR (100) NULL,
    [ShiftFactor]        NVARCHAR (100) NULL,
    [ConfigurationType]  NVARCHAR (100) NULL,
    CONSTRAINT [CPQ_Generic_PK] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CPQ_Line_Id_FK] FOREIGN KEY ([LineId]) REFERENCES [dbo].[CPQ_Line] ([Id])
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [CPQ_Generic_UC]
    ON [dbo].[CPQ_Generic]([GenericCode] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CPQ_Generic_LineId]
    ON [dbo].[CPQ_Generic]([LineId] ASC);

