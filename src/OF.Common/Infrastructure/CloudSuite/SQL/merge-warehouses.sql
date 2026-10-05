MERGE INTO [dbo].[WarehouseItems] AS Target
USING [dbo].[WarehouseItemsStaging] AS Source
ON Target.[WarehouseCode] = Source.[WarehouseCode]
WHEN MATCHED THEN
    UPDATE SET
        Target.[Warehouse] = Source.[Warehouse],
        Target.[DivisionCode] = Source.[DivisionCode],
        Target.[FacilityCode] = Source.[FacilityCode],
        Target.[Facility] = Source.[Facility],
        Target.[CountryCode] = Source.[CountryCode],
        Target.[Country] = Source.[Country]
WHEN NOT MATCHED BY TARGET THEN
    INSERT (
        [WarehouseCode],
        [Warehouse],
        [DivisionCode],
        [FacilityCode],
        [Facility],
        [CountryCode],
        [Country]
    )
    VALUES (
        Source.[WarehouseCode],
        Source.[Warehouse],
        Source.[DivisionCode],
        Source.[FacilityCode],
        Source.[Facility],
        Source.[CountryCode],
        Source.[Country]
    );