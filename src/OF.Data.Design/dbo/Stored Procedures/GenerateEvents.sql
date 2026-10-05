-- RemovedStock UPPER(a.Status) database is case insensitive, the same number of results return using upper or not.  use of upper disabled index use.
-- Use @Today variable instead of several GETDATE() calls (avoiding function calls inside predicates)

CREATE PROCEDURE [dbo].[GenerateEvents]
    @startDate DATETIME,
    @endDate DATETIME,
    @divisions NVARCHAR(1000),
    @excludedStatuses NVARCHAR(1000) = 'RemovedStock'
AS
BEGIN
    SET NOCOUNT ON;

    -- Cache current date once
    DECLARE @Today DATETIME = GETUTCDATE();

    -- Table variable for divisions
    DECLARE @divs TABLE (code NVARCHAR(5) PRIMARY KEY);

    INSERT INTO @divs(code)
    SELECT TRIM(value)
    FROM STRING_SPLIT(@divisions, ';')
    WHERE RTRIM(value) <> '';

    -- Table variable for excluded statuses
    DECLARE @excludedStatusList TABLE (status NVARCHAR(20) PRIMARY KEY);

    INSERT INTO @excludedStatusList(status)
    SELECT TRIM(value)
    FROM STRING_SPLIT(@excludedStatuses, ';')
    WHERE RTRIM(value) <> '';

    -- Main query combining event types
    SELECT
        a.Id,
        a.EventType,
        a.EventText,
        a.CssClass,
        a.StartDate,
        a.EndDate
    FROM
    (
        -- ONHIRE Events
        SELECT 
            a.Id,
            2 AS Priority,
            'ONHIRE' AS EventType,
            a.AgreementNumber + ' (' + a.CustomerName + ')' AS EventText,
            'onhire_event' AS CssClass,
            a.DeliveryDate AS StartDate,
            CASE 
                WHEN COALESCE(a.TerminationDate, a.AgreementLineValidToDate) < @Today THEN @Today 
                ELSE COALESCE(a.TerminationDate, a.AgreementLineValidToDate) 
            END AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        WHERE a.Status = 'ONHIRE' 
          AND a.AgreementNumber IS NOT NULL
          AND a.DeliveryDate <= @endDate
          AND @startDate <= CASE 
                                WHEN COALESCE(a.TerminationDate, a.AgreementLineValidToDate) < @Today THEN @Today 
                                ELSE COALESCE(a.TerminationDate, a.AgreementLineValidToDate) 
                            END

        UNION

        -- ON HOLD Events
        SELECT 
            a.Id,
            4 AS Priority,
            'ONHOLD' AS EventType,
            'ON HOLD' AS EventText,
            'onhold_event' AS CssClass,
            @startDate AS StartDate,
            @endDate AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        WHERE a.Status = 'ONHIRE' 
          AND (a.AgreementNumber IS NULL OR a.AgreementNumber = '')

        UNION

        -- SERVICE Events
        SELECT 
            a.Id,
            3 AS Priority,
            'SERVICE' AS EventType,
            'SERVICE' AS EventText,
            'service_event' AS CssClass,
            a.IONLastModified AS StartDate,
            CASE 
                WHEN a.EstimatedReadyDate IS NOT NULL AND a.EstimatedReadyDate >= a.IONLastModified THEN a.EstimatedReadyDate 
                ELSE DATEADD(day, 3, @Today) 
            END AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        WHERE a.Status IN ('SERVICE', 'ASSESS', 'INSERVICE')

        UNION

        -- TRANSPORT Events
        SELECT 
            a.Id,
            3 AS Priority,
            'TRANSPORT' AS EventType,
            'TRANSPORT' AS EventText,
            'transport_event' AS CssClass,
            a.IONLastModified AS StartDate,
            DATEADD(day, 3, a.IONLastModified) AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        WHERE a.Status = 'INTRANSIT'

        UNION

        -- COLLECTION Events
        SELECT 
            a.Id,
            3 AS Priority,
            'COLLECTION' AS EventType,
            CASE 
                WHEN (a.AgreementNumber IS NULL OR a.AgreementNumber = '') THEN 'COLLECTION' 
                ELSE a.AgreementNumber + ' (' + a.CustomerName + ')' 
            END AS EventText,
            'collection_event' AS CssClass,
            COALESCE(a.DeliveryDate, a.AgreementLineValidFromDate) AS StartDate,
            CASE 
                WHEN a.CollectionDate IS NOT NULL AND a.CollectionDate > @Today THEN a.CollectionDate 
                ELSE DATEADD(day, 3, @Today) 
            END AS EndDate
        FROM Assets a
        WHERE a.Status = 'COLLECTION'

        UNION

        -- REPAIR Events
        SELECT 
            a.Id,
            3 AS Priority,
            'REPAIR' AS EventType,
            'MAJOR REPAIR' AS EventText,
            'repair_event' AS CssClass,
            a.IONLastModified AS StartDate,
            CASE 
                WHEN a.EstimatedReadyDate IS NOT NULL AND a.EstimatedReadyDate >= a.IONLastModified THEN a.EstimatedReadyDate 
                ELSE @endDate 
            END AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        WHERE a.Status = 'REPAIR'

        UNION

        -- RESERVED Events
        SELECT 
            a.Id,
            1 AS Priority,
            'RESERVED' AS EventType,
            h.AgreementNumber + ' (' + h.CustomerName + ')' AS EventText,
            'reserved_event' AS CssClass,
            COALESCE(l.DeliveryDate, l.ValidFromDate) AS StartDate,
            COALESCE(l.CollectionDate, l.TerminationDate, l.ValidToDate) AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        INNER JOIN Reservations r ON r.AssetId = a.Id AND r.IsConfirmed = 0
        INNER JOIN Lines l ON l.Id = r.LineId
        INNER JOIN Headers h ON h.Id = l.HeaderId
        WHERE NOT EXISTS (SELECT 1 FROM @excludedStatusList es WHERE es.status = a.Status)
          AND COALESCE(l.DeliveryDate, l.ValidFromDate) <= @endDate 
          AND @startDate <= COALESCE(l.CollectionDate, l.TerminationDate, l.ValidToDate)

        UNION

        -- RINGFENCE Events
        SELECT 
            a.Id,
            1 AS Priority,
            'RINGFENCE' AS EventType,
            rf.Title AS EventText,
            'ringfence_event' AS CssClass,
            rf.FromDate AS StartDate,
            rf.ToDate AS EndDate
        FROM Assets a
        INNER JOIN @divs d ON d.code = a.Division
        INNER JOIN RingFenceItems rfi ON rfi.AssetId = a.Id
        INNER JOIN RingFences rf ON rf.Id = rfi.RingfenceId
        WHERE NOT EXISTS (SELECT 1 FROM @excludedStatusList es WHERE es.status = a.Status)
          AND rf.FromDate <= @endDate 
          AND @startDate <= rf.ToDate
    ) a
    ORDER BY a.Id, a.Priority DESC, DATEDIFF(day, a.StartDate, a.EndDate) DESC;

END
GO