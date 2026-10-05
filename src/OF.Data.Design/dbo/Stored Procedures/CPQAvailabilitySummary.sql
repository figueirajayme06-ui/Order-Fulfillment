
CREATE PROCEDURE [dbo].[CPQAvailabilitySummary]
@genericCode NVARCHAR(100),
@attributes NVARCHAR(MAX),
@startDate DATETIME,
@endDate DATETIME,
@division NVARCHAR(10)
AS
-- Parse attributes
DECLARE @att_table TABLE(name NVARCHAR(200), value NVARCHAR(200))
INSERT @att_table(name, value)
SELECT TRIM(SUBSTRING(value,1,CHARINDEX(':',value)-1)),TRIM(SUBSTRING(value,CHARINDEX(':', value)+1,LEN(value)))
FROM STRING_SPLIT(@attributes,';')
WHERE RTRIM(value) <> '';

-- Translate attributes
WITH CTE AS (
    SELECT 
        a.name,
        al.name AS TranslatedName,
        ROW_NUMBER() OVER (PARTITION BY a.name ORDER BY al.Translation) AS rn
    FROM @att_table a
    INNER JOIN AttributeLanguage al ON al.Translation = a.name
)
UPDATE a
SET a.name = c.TranslatedName
FROM @att_table a
INNER JOIN CTE c ON a.name = c.name
WHERE c.rn = 1

-- Parse divisions
DECLARE @divs TABLE(code nvarchar(5))
INSERT @divs(code)
SELECT @division

-- Create a list of all generics
DECLARE @generics TABLE(Code NVARCHAR(200), Description NVARCHAR(500), SubstitutionReason NVARCHAR(200) NULL, NumberNeeded INT)

DECLARE @itemNumber NVARCHAR(200)
DECLARE @cpqReservedItemIds TABLE(id int)
DECLARE @reservationGenericCode NVARCHAR(200)

INSERT @generics(Code, Description, SubstitutionReason, NumberNeeded)
SELECT GenericCode, GenericDescription, NULL, 1
FROM CPQ_Generic WHERE GenericCode like @genericCode + '%' OR (@reservationGenericCode IS NOT NULL AND GenericCode like @reservationGenericCode + '%')

INSERT @generics(code, Description, SubstitutionReason, NumberNeeded)
SELECT DISTINCT g2.GenericCode, g2.GenericDescription, s.Purpose, CASE WHEN CONVERT(decimal,g2.Rating_Intl)<CONVERT(decimal,g.Rating_Intl) THEN CEILING(CONVERT(decimal,g.Rating_Intl)/CONVERT(decimal,g2.Rating_Intl)) ELSE 1 END
FROM CPQ_Generic g
INNER JOIN CPQ_GenericSubstitution s ON g.Id=s.ParentGenericId
INNER JOIN CPQ_Generic g2 ON g2.Id=s.ChildGenericId
WHERE g.GenericCode=@genericCode

IF EXISTS(SELECT * FROM @att_table)
BEGIN
	DECLARE @matchingItems TABLE(ItemID INT)
	DECLARE @att_count INT
	SELECT @att_count=COUNT(*) FROM @att_table

	INSERT @matchingItems(ItemID)
	SELECT id
	FROM @cpqReservedItemIds
	UNION
	SELECT i.Id
	FROM CPQ_Generic g
	INNER JOIN @generics g2 ON g.GenericCode = g2.Code
	INNER JOIN CPQ_Item i ON i.GenericId = g.Id
	INNER JOIN CPQ_ItemAttributeValue iav ON iav.ItemId = i.Id
	INNER JOIN CPQ_Attribute att ON iav.AttributeId = att.Id
	INNER JOIN CPQ_Line line ON line.Id = g.LineId
	INNER JOIN CPQ_LineAttributePurpose lap ON lap.AttributeId=att.Id AND lap.LineId=line.Id
	INNER JOIN @att_table ia ON ia.name = att.AttributeName AND CASE WHEN att.DataType = 'Number' THEN CASE WHEN CONVERT(decimal,iav.value)>=CONVERT(decimal,ia.value) THEN 1 ELSE 0 END WHEN att.DataType='Multi' AND ';'+iav.value+';' LIKE '%;'+ia.value+';%' THEN 1 WHEN ia.value=iav.value THEN 1 ELSE 0 END = 1
	WHERE lap.Active='TRUE' AND lap.PurposeId=4 -- FAM attribute 
	GROUP BY i.Id
	HAVING COUNT(*)=@att_count

	-- Get results
	SELECT DISTINCT
	w.WarehouseCode
	,w.Warehouse
	,i.ItemNumber
	,i.DescriptionIntl
	,SUM(CASE WHEN rf.Id IS NULL AND a.Status = 'Available' THEN 1 ELSE 0 END)  as Available
	,COUNT(a.Id) as Count
	FROM Assets a
	INNER JOIN @divs d ON d.code = a.Division
	INNER JOIN WarehouseItems w ON w.WarehouseCode = a.Warehouse AND NOT w.Warehouse like 'zz%'
	INNER JOIN CPQ_Item i ON a.ItemNumber = i.ItemNumber
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	INNER JOIN @generics g2 ON g2.Code = g.GenericCode
	LEFT JOIN @matchingItems m ON m.ItemId = i.Id
	LEFT JOIN Reservations r ON r.AssetId = a.Id
	LEFT JOIN Lines l ON l.Id = r.LineId
	LEFT JOIN Headers h ON h.Id = l.HeaderId
	LEFT JOIN RingFenceItems rfi ON rfi.AssetId = a.Id
	LEFT JOIN Ringfences rf ON rf.Id = rfi.RingfenceId
	WHERE g.Active=1 AND g.Deleted=0 AND w.WarehouseCode LIKE '%0'
	GROUP BY w.WarehouseCode, w.Warehouse, i.ItemNumber, i.DescriptionIntl
	ORDER BY w.WarehouseCode
END
ELSE
BEGIN

	-- Get results
	SELECT DISTINCT
	w.WarehouseCode
	,w.Warehouse
	,i.ItemNumber
	,i.DescriptionIntl
	,SUM(CASE WHEN rf.Id IS NULL AND a.Status = 'Available' THEN 1 ELSE 0 END)  as Available
	,COUNT(a.Id) as Count
	FROM Assets a
	INNER JOIN @divs d ON d.code = a.Division
	INNER JOIN WarehouseItems w ON w.WarehouseCode = a.Warehouse AND NOT w.Warehouse like 'zz%'
	INNER JOIN CPQ_Item i ON a.ItemNumber = i.ItemNumber
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	INNER JOIN @generics g2 ON g2.Code = g.GenericCode
	LEFT JOIN Reservations r ON r.AssetId = a.Id
	LEFT JOIN Lines l ON l.Id = r.LineId
	LEFT JOIN Headers h ON h.Id = l.HeaderId
	LEFT JOIN RingFenceItems rfi ON rfi.AssetId = a.Id
	LEFT JOIN Ringfences rf ON rf.Id = rfi.RingfenceId
	WHERE g.Active=1 AND g.Deleted=0
	GROUP BY w.WarehouseCode, w.Warehouse, i.ItemNumber, i.DescriptionIntl
	ORDER BY w.WarehouseCode
END
GO


