-- Order Fulfilment availability contract.
--
-- This procedure is used by the NOF agreement availability workbench. It has a
-- deliberately separate contract from dbo.GetCPQAvailabilitySummary: it returns
-- ReservationMode, includes every configured generic substitution purpose, and
-- subtracts overlapping pending quantity reservations from ProductItems stock.
CREATE PROCEDURE [dbo].[GetFulfilmentAvailabilitySummary]
(
    @genericCode NVARCHAR(255),
    @attributes NVARCHAR(MAX),
    @startDate DATETIME2,
    @endDate DATETIME2,
    @divisions NVARCHAR(MAX),
    @itemNumber NVARCHAR(255) = NULL,
    @lineId INT = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    -- The agreement workflow normally supplies both dates. Treat a missing bound
    -- as open-ended so direct callers cannot overstate availability by omitting it.
    DECLARE @rangeStart DATE = CONVERT(DATE, @startDate);
    DECLARE @rangeEnd DATE = CONVERT(DATE, @endDate);
    DECLARE @minDate DATE = DATEFROMPARTS(1, 1, 1);
    DECLARE @maxDate DATE = DATEFROMPARTS(9999, 12, 31);

    IF @rangeStart IS NULL
        SET @rangeStart = @minDate;

    IF @rangeEnd IS NULL
        SET @rangeEnd = @maxDate;

    IF @rangeEnd < @rangeStart
        SET @rangeEnd = @rangeStart;

    -- Parse attributes.
    DECLARE @att_count INT;
    DECLARE @att_table TABLE
    (
        name NVARCHAR(200),
        value NVARCHAR(200),
        attributeId INT,
        dataType NVARCHAR(50)
    );

    INSERT INTO @att_table (name, value, attributeId, dataType)
    SELECT [Name], [Value], Id, DataType
    FROM dbo.AttributeStringToTable(@attributes)
    WHERE [Value] <> 'No';

    SELECT @att_count = COUNT(*) FROM @att_table;

    -- Parse and de-duplicate the divisions already authorised by the Web API.
    DECLARE @division_table TABLE
    (
        DivisionCode NVARCHAR(255) PRIMARY KEY,
        DivisionName NVARCHAR(255)
    );

    INSERT INTO @division_table (DivisionCode)
    SELECT DISTINCT TRIM([value])
    FROM STRING_SPLIT(@divisions, ',')
    WHERE TRIM([value]) <> '';

    UPDATE division
    SET division.DivisionName = warehouse.Country
    FROM @division_table division
    OUTER APPLY
    (
        SELECT TOP 1 wi.Country
        FROM dbo.WarehouseItems wi
        WHERE wi.DivisionCode = division.DivisionCode
            AND UPPER(wi.Warehouse) NOT LIKE '%DO NOT USE%'
            AND UPPER(wi.Warehouse) NOT LIKE '%NOT FOR USE%'
            AND UPPER(wi.Warehouse) NOT LIKE 'ZZ%'
            AND wi.DivisionCode <> '250'
    ) warehouse;

    -- Mirror FulfilNonSerialized's line-based substitution context. The quoted
    -- item determines the primary generic. Lines.GenericItemNumber is only an
    -- additional generic after an active non-depot reservation exists.
    DECLARE @effectiveItemNumber NVARCHAR(255) = NULLIF(TRIM(@itemNumber), '');
    DECLARE @fulfilmentGenericCode NVARCHAR(255);
    DECLARE @reservationGenericCode NVARCHAR(255);

    IF @lineId IS NOT NULL
    BEGIN
        SELECT @effectiveItemNumber = COALESCE(NULLIF(TRIM(line.ItemNumber), ''), @effectiveItemNumber)
        FROM dbo.Lines line
        WHERE line.Id = @lineId;

        SELECT TOP 1 @reservationGenericCode = line.GenericItemNumber
        FROM dbo.Lines line
        INNER JOIN dbo.Reservations reservation ON reservation.LineId = line.Id
        LEFT JOIN dbo.Headers header ON header.Id = line.HeaderId
        WHERE line.Id = @lineId
            AND reservation.IsDepotFulfilled = 0
            AND line.IsDeleted = 0
            AND (line.HeaderId IS NULL OR header.IsDeleted = 0);
    END;

    IF @effectiveItemNumber IS NULL
        SET @effectiveItemNumber = @genericCode;

    SELECT @fulfilmentGenericCode = generic.GenericCode
    FROM dbo.CPQ_Item item
    INNER JOIN dbo.CPQ_Generic generic ON generic.Id = item.GenericId
    WHERE item.ItemNumber = @effectiveItemNumber;

    IF @fulfilmentGenericCode IS NULL
        SET @fulfilmentGenericCode = @effectiveItemNumber;

    -- NOF intentionally includes every configured generic substitution purpose.
    DECLARE @generics TABLE
    (
        Id INT,
        GenericCode NVARCHAR(200),
        Description NVARCHAR(500)
    );

    INSERT INTO @generics (Id, GenericCode, Description)
    SELECT matches.Id, matches.GenericCode, matches.GenericDescription
    FROM dbo.GetFulfilmentGenericsWithSubstitutions(@fulfilmentGenericCode) matches;

    INSERT INTO @generics (Id, GenericCode, Description)
    SELECT generic.Id, generic.GenericCode, generic.GenericDescription
    FROM dbo.CPQ_Generic generic
    WHERE @reservationGenericCode IS NOT NULL
        AND generic.GenericCode = @reservationGenericCode
        AND generic.Active = 1
        AND generic.Deleted = 0
        AND NOT EXISTS
        (
            SELECT 1 FROM @generics existing WHERE existing.Id = generic.Id
        );

    DECLARE @relatedSpecificItems TABLE
    (
        ChildItemId INT PRIMARY KEY
    );

    INSERT INTO @relatedSpecificItems (ChildItemId)
    SELECT DISTINCT related.ChildItemId
    FROM dbo.CPQ_RelatedSpecificSubstitution related
    INNER JOIN dbo.CPQ_Item parentItem ON parentItem.Id = related.ParentItemId
    INNER JOIN dbo.CPQ_Generic parentGeneric ON parentGeneric.Id = parentItem.GenericId
    WHERE parentGeneric.GenericCode = @fulfilmentGenericCode
        OR (@reservationGenericCode IS NOT NULL
            AND parentGeneric.GenericCode = @reservationGenericCode)
        OR parentItem.ItemNumber = @effectiveItemNumber;

    -- Keep ordinary/generic-substitution items separate from related items so a
    -- child already returned by a generic substitution is neither duplicated
    -- nor relabelled as a related substitute.
    DECLARE @availabilityItems TABLE
    (
        ItemId INT PRIMARY KEY,
        SubstitutionReason NVARCHAR(50) NULL
    );

    INSERT INTO @availabilityItems (ItemId, SubstitutionReason)
    SELECT DISTINCT item.Id, NULL
    FROM dbo.CPQ_Item item
    INNER JOIN @generics generic ON generic.Id = item.GenericId;

    INSERT INTO @availabilityItems (ItemId, SubstitutionReason)
    SELECT DISTINCT childItem.Id, N'RELATED'
    FROM @relatedSpecificItems relatedItem
    INNER JOIN dbo.CPQ_Item childItem ON childItem.Id = relatedItem.ChildItemId
    INNER JOIN dbo.CPQ_Generic childGeneric ON childGeneric.Id = childItem.GenericId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM @generics generic
        WHERE generic.GenericCode = childGeneric.GenericCode
    )
        AND childGeneric.Active = 1
        AND childGeneric.Deleted = 0;

    -- Determine availability for serialized assets.
    DECLARE @asset_availability TABLE
    (
        AssetId NVARCHAR(200) PRIMARY KEY,
        IsAvailableForPeriod BIT,
        HasOverlappingReservation BIT,
        HasOverlappingRingfence BIT
    );

    INSERT INTO @asset_availability
        (AssetId, IsAvailableForPeriod, HasOverlappingReservation, HasOverlappingRingfence)
    SELECT
        asset.Id,
        -- Current status is a point-in-time feed. A dated OnHire asset can cover
        -- a later period only after its inclusive current-agreement release date.
        -- Keep undated/on-hold and other operational states unavailable.
        MAX(CASE
            WHEN asset.Status = 'Available' THEN 1
            WHEN asset.Status = 'OnHire'
                AND NULLIF(TRIM(asset.AgreementNumber), '') IS NOT NULL
                AND COALESCE(
                    CONVERT(DATE, asset.CollectionDate),
                    CONVERT(DATE, asset.TerminationDate),
                    CONVERT(DATE, asset.AgreementLineValidToDate)) < @rangeStart
            THEN 1 ELSE 0
        END),
        MAX(CASE
            WHEN reservation.AssetId IS NOT NULL
                AND CONVERT(DATE, COALESCE(line.DeliveryDate, line.ValidFromDate)) <= @rangeEnd
                AND @rangeStart <= CONVERT(DATE, COALESCE(line.TerminationDate, line.ValidToDate))
            THEN 1 ELSE 0
        END),
        MAX(CASE
            WHEN ringfence.Id IS NOT NULL
                AND CONVERT(DATE, ringfence.FromDate) <= @rangeEnd
                AND @rangeStart <= CONVERT(DATE, ringfence.ToDate)
            THEN 1 ELSE 0
        END)
    FROM dbo.Assets asset
    INNER JOIN dbo.CPQ_Item item ON asset.ItemNumber = item.ItemNumber
    INNER JOIN @availabilityItems availabilityItem ON availabilityItem.ItemId = item.Id
    INNER JOIN @division_table division ON asset.Division = division.DivisionCode
    LEFT JOIN dbo.Reservations reservation ON reservation.AssetId = asset.Id
    LEFT JOIN dbo.Lines line ON line.Id = reservation.LineId
    LEFT JOIN dbo.RingFenceItems ringfenceItem ON ringfenceItem.AssetId = asset.Id
    LEFT JOIN dbo.Ringfences ringfence ON ringfence.Id = ringfenceItem.RingfenceId
    GROUP BY asset.Id;

    -- Find items matching all requested attributes.
    DECLARE @matchingItems TABLE (ItemID INT PRIMARY KEY);

    INSERT INTO @matchingItems (ItemID)
    SELECT availabilityItem.ItemId
    FROM @availabilityItems availabilityItem
    INNER JOIN dbo.CPQ_Item item ON item.Id = availabilityItem.ItemId
    INNER JOIN dbo.CPQ_ItemAttributeValue itemAttribute ON itemAttribute.ItemId = item.Id
    INNER JOIN @att_table requestedAttribute ON requestedAttribute.AttributeId = itemAttribute.AttributeId
    GROUP BY availabilityItem.ItemId
    HAVING SUM(CASE
        WHEN requestedAttribute.DataType = 'Number'
            THEN CASE
                WHEN TRY_CONVERT(DECIMAL, itemAttribute.value) >= TRY_CONVERT(DECIMAL, requestedAttribute.value)
                THEN 1 ELSE 0
            END
        WHEN requestedAttribute.DataType = 'Multi'
            AND ';' + itemAttribute.value + ';' LIKE '%;' + requestedAttribute.value + ';%'
            THEN 1
        WHEN requestedAttribute.value = itemAttribute.value THEN 1
        ELSE 0
    END) >= @att_count;

    DECLARE @item_numbers TABLE (ItemNumber NVARCHAR(200) PRIMARY KEY);

    INSERT INTO @item_numbers (ItemNumber)
    SELECT item.ItemNumber
    FROM dbo.CPQ_Item item
    INNER JOIN @availabilityItems availabilityItem ON availabilityItem.ItemId = item.Id
    WHERE EXISTS
    (
        SELECT 1 FROM dbo.ProductItems product WHERE product.ItemNumber = item.ItemNumber
    )
        OR EXISTS
    (
        SELECT 1 FROM dbo.Assets asset WHERE asset.ItemNumber = item.ItemNumber
    );

    -- Match the reservation command's maximum-concurrent-pending calculation.
    -- Confirmed reservations are excluded because upstream AllocatedQuantity is
    -- expected to include them.
    DECLARE @quantity_reservation_peaks TABLE
    (
        ItemNumber NVARCHAR(200),
        Warehouse NVARCHAR(100),
        PeakPending DECIMAL(38, 2),
        PRIMARY KEY (ItemNumber, Warehouse)
    );

    ;WITH CandidateStock AS
    (
        SELECT product.ItemNumber, product.Warehouse
        FROM dbo.ProductItems product
        INNER JOIN @item_numbers item ON item.ItemNumber = product.ItemNumber
        INNER JOIN @division_table division ON division.DivisionCode = product.Division
        WHERE product.Status NOT IN ('RemovedStock', 'Scrap', 'Sold')
    ),
    ReservationIntervals AS
    (
        SELECT
            stock.ItemNumber,
            stock.Warehouse,
            reservation.Id,
            Quantity = CONVERT(DECIMAL(38, 2), reservation.Quantity),
            ReservationStart = COALESCE(
                CONVERT(DATE, line.DeliveryDate),
                CONVERT(DATE, line.ValidFromDate),
                @minDate),
            ReservationEnd = COALESCE(
                CONVERT(DATE, line.TerminationDate),
                CONVERT(DATE, line.ValidToDate),
                @maxDate)
        FROM CandidateStock stock
        INNER JOIN dbo.Reservations reservation
            ON reservation.ItemNumber = stock.ItemNumber
            AND reservation.Warehouse = stock.Warehouse
            AND reservation.IsConfirmed = 0
        LEFT JOIN dbo.Lines line ON line.Id = reservation.LineId
    ),
    OverlappingReservations AS
    (
        SELECT
            ItemNumber,
            Warehouse,
            Id,
            Quantity,
            EffectiveStart = CASE
                WHEN ReservationStart < @rangeStart THEN @rangeStart
                ELSE ReservationStart
            END,
            EffectiveEnd = CASE
                WHEN ReservationEnd > @rangeEnd THEN @rangeEnd
                ELSE ReservationEnd
            END
        FROM ReservationIntervals
        WHERE ReservationEnd >= @rangeStart
            AND ReservationStart <= @rangeEnd
    ),
    ReservationEvents AS
    (
        SELECT ItemNumber, Warehouse, EventDate = EffectiveStart, Delta = Quantity
        FROM OverlappingReservations

        UNION ALL

        SELECT
            ItemNumber,
            Warehouse,
            EventDate = DATEADD(DAY, 1, EffectiveEnd),
            Delta = -Quantity
        FROM OverlappingReservations
        WHERE EffectiveEnd < @maxDate
    ),
    EventsPerDate AS
    (
        SELECT ItemNumber, Warehouse, EventDate, Delta = SUM(Delta)
        FROM ReservationEvents
        GROUP BY ItemNumber, Warehouse, EventDate
    ),
    RunningReservations AS
    (
        SELECT
            ItemNumber,
            Warehouse,
            RunningQuantity = SUM(Delta) OVER
            (
                PARTITION BY ItemNumber, Warehouse
                ORDER BY EventDate
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
            )
        FROM EventsPerDate
    )
    INSERT INTO @quantity_reservation_peaks (ItemNumber, Warehouse, PeakPending)
    SELECT
        ItemNumber,
        Warehouse,
        CASE
            WHEN MAX(RunningQuantity) > 0 THEN MAX(RunningQuantity)
            ELSE CONVERT(DECIMAL(38, 2), 0)
        END
    FROM RunningReservations
    GROUP BY ItemNumber, Warehouse;

    DECLARE @results TABLE
    (
        WarehouseCode NVARCHAR(100),
        Warehouse NVARCHAR(100),
        GenericCode NVARCHAR(200),
        GenericDescription NVARCHAR(500),
        ItemNumber NVARCHAR(200),
        DescriptionIntl NVARCHAR(500),
        Facility NVARCHAR(100),
        DivisionCode NVARCHAR(255),
        DivisionName NVARCHAR(255),
        Available INT,
        [Count] INT,
        GenericOnly BIT,
        ReservationMode NVARCHAR(20),
        SubstitutionReason NVARCHAR(50) NULL
    );

    INSERT INTO @results
    SELECT
        warehouse.WarehouseCode,
        warehouse.Warehouse,
        generic.GenericCode,
        generic.GenericDescription,
        item.ItemNumber,
        item.DescriptionIntl,
        warehouse.FacilityCode,
        division.DivisionCode,
        division.DivisionName,
        SUM(CASE
            WHEN ISNULL(availability.IsAvailableForPeriod, 0) = 1
                AND ISNULL(availability.HasOverlappingReservation, 0) = 0
                AND ISNULL(availability.HasOverlappingRingfence, 0) = 0
            THEN 1 ELSE 0
        END),
        COUNT(asset.Id),
        CAST(0 AS BIT),
        'asset',
        availabilityItem.SubstitutionReason
    FROM dbo.Assets asset
    INNER JOIN dbo.CPQ_Item item ON item.ItemNumber = asset.ItemNumber
    INNER JOIN @availabilityItems availabilityItem ON availabilityItem.ItemId = item.Id
    INNER JOIN dbo.CPQ_Generic generic ON generic.Id = item.GenericId
    INNER JOIN dbo.WarehouseItems warehouse
        ON warehouse.WarehouseCode = asset.Warehouse
        AND UPPER(warehouse.Warehouse) NOT LIKE 'ZZ%'
    INNER JOIN @division_table division
        ON division.DivisionCode = asset.Division
        AND warehouse.DivisionCode = division.DivisionCode
    LEFT JOIN @matchingItems matchingItem ON matchingItem.ItemID = item.Id
    LEFT JOIN @asset_availability availability ON availability.AssetId = asset.Id
    WHERE @att_count = 0 OR matchingItem.ItemID IS NOT NULL
    GROUP BY
        warehouse.WarehouseCode,
        warehouse.Warehouse,
        generic.GenericCode,
        generic.GenericDescription,
        item.ItemNumber,
        item.DescriptionIntl,
        warehouse.FacilityCode,
        division.DivisionCode,
        division.DivisionName,
        availabilityItem.SubstitutionReason;

    ;WITH QuantityRows AS
    (
        SELECT
            warehouse.WarehouseCode,
            warehouse.Warehouse,
            generic.GenericCode,
            GenericDescription = generic.GenericDescription,
            item.ItemNumber,
            item.DescriptionIntl,
            availabilityItem.SubstitutionReason,
            Facility = warehouse.FacilityCode,
            division.DivisionCode,
            division.DivisionName,
            BaseAvailable = SUM(
                CONVERT(DECIMAL(28, 2), product.StockQuantity)
                - CONVERT(DECIMAL(28, 2), product.AllocatedQuantity)),
            TotalStock = SUM(CONVERT(DECIMAL(28, 2), product.StockQuantity)),
            PeakPending = COALESCE(
                MAX(reservationPeak.PeakPending),
                CONVERT(DECIMAL(38, 2), 0))
        FROM dbo.ProductItems product
        INNER JOIN dbo.CPQ_Item item ON item.ItemNumber = product.ItemNumber
        INNER JOIN @availabilityItems availabilityItem ON availabilityItem.ItemId = item.Id
        INNER JOIN dbo.CPQ_Generic generic ON generic.Id = item.GenericId
        INNER JOIN dbo.WarehouseItems warehouse
            ON warehouse.WarehouseCode = product.Warehouse
            AND UPPER(warehouse.Warehouse) NOT LIKE 'ZZ%'
        INNER JOIN @division_table division
            ON division.DivisionCode = product.Division
            AND warehouse.DivisionCode = division.DivisionCode
        LEFT JOIN @matchingItems matchingItem ON matchingItem.ItemID = item.Id
        LEFT JOIN @quantity_reservation_peaks reservationPeak
            ON reservationPeak.ItemNumber = product.ItemNumber
            AND reservationPeak.Warehouse = product.Warehouse
        WHERE (@att_count = 0 OR matchingItem.ItemID IS NOT NULL)
            AND product.Status NOT IN ('RemovedStock', 'Scrap', 'Sold')
        GROUP BY
            warehouse.WarehouseCode,
            warehouse.Warehouse,
            generic.GenericCode,
            generic.GenericDescription,
            item.ItemNumber,
            item.DescriptionIntl,
            availabilityItem.SubstitutionReason,
            warehouse.FacilityCode,
            division.DivisionCode,
            division.DivisionName
    )
    INSERT INTO @results
    SELECT
        quantityRow.WarehouseCode,
        quantityRow.Warehouse,
        quantityRow.GenericCode,
        quantityRow.GenericDescription,
        quantityRow.ItemNumber,
        quantityRow.DescriptionIntl,
        quantityRow.Facility,
        quantityRow.DivisionCode,
        quantityRow.DivisionName,
        CASE WHEN converted.Available < 0 THEN 0 ELSE converted.Available END,
        CONVERT(INT, FLOOR(quantityRow.TotalStock)),
        CAST(0 AS BIT),
        'quantity',
        quantityRow.SubstitutionReason
    FROM QuantityRows quantityRow
    CROSS APPLY
    (
        VALUES
        (
            CONVERT(INT, FLOOR(
                CONVERT(DECIMAL(28, 2), quantityRow.BaseAvailable)
                - CONVERT(DECIMAL(28, 2), quantityRow.PeakPending)))
        )
    ) converted(Available);

    -- Rehire and XXMISC rows are scoped to the generic requested by NOF.
    INSERT INTO @results
    SELECT
        'N/A',
        'N/A',
        generic.GenericCode,
        generic.GenericDescription,
        '',
        generic.GenericDescription,
        '',
        '',
        '',
        999,
        999,
        CAST(1 AS BIT),
        'generic',
        NULL
    FROM dbo.CPQ_Generic generic
    WHERE @att_count = 0
        AND generic.Active = 1
        AND generic.Deleted = 0
        AND generic.GenericCode = @fulfilmentGenericCode
        AND (generic.Rehire = 'Yes' OR generic.GenericCode LIKE 'XXMISC%');

    SELECT
        WarehouseCode,
        Warehouse,
        GenericCode,
        GenericDescription,
        ItemNumber,
        DescriptionIntl,
        Facility,
        DivisionCode,
        DivisionName,
        Available,
        [Count],
        GenericOnly,
        ReservationMode,
        SubstitutionReason
    FROM @results
    ORDER BY GenericCode, ItemNumber, WarehouseCode;
END
