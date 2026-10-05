-- CPQ Next compatibility contract.
--
-- This procedure supports CPQ Next catalogue and availability searches. Keep its
-- parameters, result shape, and Substitute Up policy backward compatible. The
-- Order Fulfilment application uses dbo.GetFulfilmentAvailabilitySummary instead.
CREATE PROCEDURE [dbo].[GetCPQAvailabilitySummary]
(
    @lineId INT,
    @attributes NVARCHAR(MAX),
    @startDate DATETIME2,
    @endDate DATETIME2,
    @genericCode NVARCHAR(255),
    @region NVARCHAR(255) = NULL,
    @descriptionSearch	NVARCHAR(500) = NULL,
    @familyId INT = NULL,
    @divisions NVARCHAR(MAX) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

     -- Parse attributes
    DECLARE @att_count INT;
    DECLARE @att_table TABLE(name NVARCHAR(200), value NVARCHAR(200), attributeId INT, dataType NVARCHAR(50));
    INSERT INTO @att_table (name, value, attributeId, dataType)
    SELECT [Name], [Value], Id, DataType
    FROM dbo.AttributeStringToTable(@attributes)
    WHERE [Value] <> 'No';

    SELECT @att_count = COUNT(*) FROM @att_table;

    -- Parse divisions
    DECLARE @division_table TABLE(DivisionCode NVARCHAR(255), DivisionName NVARCHAR(255));
    INSERT INTO @division_table (DivisionCode)
    SELECT TRIM([value]) FROM STRING_SPLIT(@divisions, ',') WHERE TRIM([value]) <> ''

    UPDATE d
    SET d.DivisionName = w.Country -- Use the country from WarehouseItems as the division name
    FROM @division_table d         -- Apply here as to not upset the grouping in the main query.
    OUTER APPLY (
        SELECT TOP 1 Country
        FROM dbo.WarehouseItems wi
        WHERE wi.DivisionCode = d.DivisionCode
        AND UPPER(wi.Warehouse) NOT LIKE '%DO NOT USE%'
        AND UPPER(wi.Warehouse) NOT LIKE '%NOT FOR USE%'
        AND UPPER(wi.Warehouse) NOT LIKE 'ZZ%'
        AND wi.DivisionCode != '250' -- Holding
    ) w;

    -- Get generics for the line, including substitutions, and filter by region if provided
    DECLARE @generics TABLE(Id INT, GenericCode NVARCHAR(200), Description NVARCHAR(500));
    INSERT INTO @generics
    SELECT Id, GenericCode, GenericDescription
    FROM dbo.GetGenericsWithSubstitutions(@familyId, @lineId, @genericCode, @region, @descriptionSearch);

    -- Determine availability for each asset based on reservations and ringfences
    DECLARE @asset_availability TABLE(AssetId NVARCHAR(200) PRIMARY KEY, HasOverlappingReservation BIT, HasOverlappingRingfence BIT);
    INSERT INTO @asset_availability (AssetId, HasOverlappingReservation, HasOverlappingRingfence)
    SELECT 
        a.Id,
        MAX(CASE 
            WHEN r.AssetId IS NOT NULL 
                AND l.ValidFromDate <= @endDate 
                AND @startDate <= COALESCE(l.TerminationDate, l.ValidToDate)
            THEN 1 ELSE 0 
        END) AS HasOverlappingReservation,
        MAX(CASE 
            WHEN rf.Id IS NOT NULL 
                AND rf.FromDate <= @endDate 
                AND @startDate <= rf.ToDate
            THEN 1 ELSE 0 
        END) AS HasOverlappingRingfence
    FROM dbo.Assets a
    INNER JOIN dbo.CPQ_Item i ON a.ItemNumber = i.ItemNumber
    INNER JOIN @generics g ON i.GenericId = g.Id
    INNER JOIN @division_table d ON a.Division = d.DivisionCode
    LEFT JOIN dbo.Reservations r ON r.AssetId = a.Id
    LEFT JOIN dbo.Lines l ON l.Id = r.LineId
    LEFT JOIN dbo.RingFenceItems rfi ON rfi.AssetId = a.Id
    LEFT JOIN dbo.Ringfences rf ON rf.Id = rfi.RingfenceId
    GROUP BY a.Id;

    -- Find items matching attribute criteria
    -- When @att_table is empty, INNER JOIN produces 0 rows (no-op)
    DECLARE @matchingItems TABLE(ItemID INT);
    INSERT INTO @matchingItems (ItemID)
    SELECT i.Id
    FROM dbo.CPQ_Generic g
    INNER JOIN @generics g2 ON g.GenericCode = g2.GenericCode
    INNER JOIN dbo.CPQ_Item i ON i.GenericId = g.Id
    INNER JOIN dbo.CPQ_ItemAttributeValue iav ON iav.ItemId = i.Id 
    INNER JOIN @att_table ia ON ia.AttributeId = iav.AttributeId
    GROUP BY i.Id
    HAVING SUM(CASE 
        WHEN ia.DataType = 'Number' 
            THEN CASE WHEN TRY_CONVERT(DECIMAL, iav.value) >= TRY_CONVERT(DECIMAL, ia.value) THEN 1 ELSE 0 END
        WHEN ia.DataType = 'Multi' AND ';' + iav.value + ';' LIKE '%;' + ia.value + ';%' 
            THEN 1
        WHEN ia.value = iav.value 
            THEN 1
        ELSE 0 
    END) >= @att_count;

    -- Collect all relevant item numbers (deduplicated via UNION)
    DECLARE @item_numbers TABLE(ItemNumber NVARCHAR(200));
    INSERT INTO @item_numbers
    SELECT DISTINCT p.ItemNumber
    FROM dbo.ProductItems p
    INNER JOIN dbo.CPQ_Item i ON p.ItemNumber = i.ItemNumber
    INNER JOIN @generics g ON g.Id = i.GenericId
    UNION
    SELECT DISTINCT a.ItemNumber
    FROM dbo.Assets a
    INNER JOIN dbo.CPQ_Item i ON a.ItemNumber = i.ItemNumber
    INNER JOIN @generics g ON g.Id = i.GenericId;

    -- Combined availability query
    -- Note: Assumes Assets and ProductItems are mutually exclusive per ItemNumber
    SELECT
        ISNULL(wa.WarehouseCode, wp.WarehouseCode) AS WarehouseCode,
        ISNULL(wa.Warehouse, wp.Warehouse) AS Warehouse,
        g.GenericCode,
        g.Description AS GenericDescription,
        i.ItemNumber,
        i.DescriptionIntl,
        ISNULL(wa.FacilityCode, wp.FacilityCode) AS Facility,
        d.DivisionCode,
        d.DivisionName,

        CASE WHEN COUNT(a.Id) = 0
            THEN CAST(SUM(p.StockQuantity - p.AllocatedQuantity) AS INT)
            ELSE SUM(CASE 
                    WHEN a.Status = 'Available' 
                        AND ISNULL(av.HasOverlappingReservation, 0) = 0
                        AND ISNULL(av.HasOverlappingRingfence, 0) = 0
                    THEN 1 ELSE 0 
                END)
        END AS Available,

        CASE WHEN COUNT(a.Id) = 0
            THEN CAST(SUM(p.StockQuantity) AS INT)
            ELSE COUNT(a.Id)
        END AS [Count],

        CAST(0 AS BIT) AS GenericOnly

    FROM @item_numbers itm
    INNER JOIN dbo.CPQ_Item i ON itm.ItemNumber = i.ItemNumber
    INNER JOIN @generics g ON g.Id = i.GenericId
    LEFT JOIN @matchingItems mi ON mi.ItemID = i.Id
    LEFT JOIN dbo.Assets a ON a.ItemNumber = itm.ItemNumber
    LEFT JOIN dbo.ProductItems p ON p.ItemNumber = itm.ItemNumber
    LEFT JOIN dbo.WarehouseItems wa ON wa.WarehouseCode = a.Warehouse AND NOT wa.Warehouse LIKE 'zz%'
    LEFT JOIN dbo.WarehouseItems wp ON wp.WarehouseCode = p.Warehouse AND NOT wp.Warehouse LIKE 'zz%'
    LEFT JOIN @division_table d ON wa.DivisionCode = d.DivisionCode OR wp.DivisionCode = d.DivisionCode
    LEFT JOIN @asset_availability av ON av.AssetId = a.Id

    WHERE (@att_count = 0 OR mi.ItemID IS NOT NULL)
        AND (d.DivisionCode IS NOT NULL)

    GROUP BY 
        wa.WarehouseCode, wa.Warehouse, 
        wp.WarehouseCode, wp.Warehouse, 
        g.GenericCode, g.Description, i.ItemNumber,
        i.DescriptionIntl, wa.FacilityCode, wp.FacilityCode, d.DivisionCode, d.DivisionName

    UNION ALL

    -- Rehire / XXMISC generics (only when no attribute filtering)
    SELECT
        'N/A'                AS WarehouseCode,
        'N/A'                AS Warehouse,
        g.GenericCode,
        g.GenericDescription AS GenericDescription,
        ''                   AS ItemNumber,
        g.GenericDescription AS DescriptionIntl,
        ''                   AS Facility,
        ''                   AS DivisionCode,
        ''                   AS DivisionName,
        999                  AS Available,
        999                  AS [Count],
        CAST(1 AS BIT)       AS GenericOnly
    FROM dbo.CPQ_Generic g
    INNER JOIN CPQ_Line l ON l.Id = g.LineId
    INNER JOIN CPQ_Family f ON f.Id = l.FamilyId
    WHERE @att_count = 0
        AND g.Active = 1 
        AND g.Deleted = 0
        AND (@familyId IS NULL OR f.Id = @familyId)
        AND (@lineId IS NULL OR g.LineId = @lineId)
        AND (g.Rehire = 'Yes' OR g.GenericCode LIKE 'XXMISC%')
    ORDER BY GenericCode, ItemNumber, WarehouseCode;
END
