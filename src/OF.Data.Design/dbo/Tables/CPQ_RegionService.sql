CREATE TABLE [dbo].[CPQ_RegionService](
    [Id]                          [uniqueidentifier] NOT NULL,
    [RegionId]                    [int]              NULL,
    [RegionServiceId]             [int]              NULL,
    [ServiceId]                   [int]              NULL,
    [StatusCode]                  [int]              NULL,
    [TimeZoneRuleVersionNumber]   [int]              NULL,
    [UTCConversionTimeZoneCode]   [int]              NULL,
    [VersionNumber]               [bigint]           NULL,
    [ImportSequenceNumber]        [int]              NULL,
    [OwningBusinessUnit]          [nvarchar](100)    NULL,
    CONSTRAINT [PK_CPQ_RegionService] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CPQ_RegionService_Region]  FOREIGN KEY ([RegionId])  REFERENCES [dbo].[CPQ_Region]([Id])  ON DELETE SET NULL,
    CONSTRAINT [FK_CPQ_RegionService_Service] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[CPQ_Service]([Id]) ON DELETE SET NULL
);