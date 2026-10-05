
CREATE PROCEDURE [dbo].[FulfilSerialized]
@lineId INT,
@attributes NVARCHAR(1000),
@divisions NVARCHAR(1000)
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
SELECT value
FROM STRING_SPLIT(@divisions,';')
WHERE RTRIM(value) <> '';

-- Force divisions for any existing reservations
INSERT @divs(code)
SELECT DISTINCT a.Division
FROM Reservations r
INNER JOIN Assets a ON a.Id = r.AssetId
LEFT JOIN @divs d ON d.code = a.Division
WHERE d.code IS NULL AND r.LineId = @lineId

-- Create a list of all generics
DECLARE @generics TABLE(Code NVARCHAR(200), Description NVARCHAR(500), SubstitutionReason NVARCHAR(200) NULL, NumberNeeded INT)

DECLARE @itemNumber NVARCHAR(200)
DECLARE @genericCode NVARCHAR(200)
DECLARE @cpqReservedItemIds TABLE(id int)
DECLARE @reservationGenericCode NVARCHAR(200)

SELECT @itemNumber = ItemNumber FROM Lines WHERE Id = @LineId
INSERT @cpqReservedItemIds(id)
SELECT DISTINCT i.Id FROM CPQ_Item i INNER JOIN Reservations r on i.ItemNumber = r.ItemNumber WHERE r.LineId = @lineId AND r.IsDepotFulfilled = 0 AND i.Id is not null
SELECT @reservationGenericCode = l.GenericItemNumber FROM Lines l INNER JOIN Reservations r on l.Id = r.LineId WHERE r.LineId = @LineId AND r.IsDepotFulfilled = 0

IF EXISTS(SELECT * FROM CPQ_Item WHERE ItemNumber=@itemNumber)
BEGIN
	SELECT @genericCode = g.GenericCode
	FROM CPQ_Item i
	INNER JOIN CPQ_Generic g ON i.GenericId=g.Id
	WHERE i.ItemNumber=@itemNumber
END
ELSE
BEGIN
	SELECT @genericCode = @itemNumber
END

INSERT @generics(Code, Description, SubstitutionReason, NumberNeeded)
SELECT GenericCode, GenericDescription, NULL, 1
FROM CPQ_Generic WHERE GenericCode like @genericCode + '%' OR (@reservationGenericCode IS NOT NULL AND GenericCode like @reservationGenericCode + '%')

INSERT @generics(code, Description, SubstitutionReason, NumberNeeded)
SELECT DISTINCT g2.GenericCode, g2.GenericDescription, s.Purpose, CASE WHEN CONVERT(decimal,g2.Rating_Intl)<CONVERT(decimal,g.Rating_Intl) THEN CEILING(CONVERT(decimal,g.Rating_Intl)/CONVERT(decimal,g2.Rating_Intl)) ELSE 1 END
FROM CPQ_Generic g
INNER JOIN CPQ_GenericSubstitution s ON g.Id=s.ParentGenericId
INNER JOIN CPQ_Generic g2 ON g2.Id=s.ChildGenericId
WHERE g.GenericCode=@genericCode

-- Create a list of related specific substitution items
DECLARE @relatedSpecificItems TABLE(ChildItemId INT, NumberNeeded INT)
INSERT @relatedSpecificItems(ChildItemId, NumberNeeded)
SELECT DISTINCT rss.ChildItemId, 1
FROM CPQ_RelatedSpecificSubstitution rss
INNER JOIN CPQ_Item p ON p.Id = rss.ParentItemId
INNER JOIN CPQ_Generic pg ON pg.Id = p.GenericId
WHERE pg.GenericCode = @genericCode
   OR (@reservationGenericCode IS NOT NULL AND pg.GenericCode = @reservationGenericCode)
   OR p.ItemNumber = @itemNumber

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

	-- Also include related specific items that match FAM attributes
	INSERT @matchingItems(ItemID)
	SELECT i.Id
	FROM @relatedSpecificItems rsi
	INNER JOIN CPQ_Item i ON i.Id = rsi.ChildItemId
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	INNER JOIN CPQ_ItemAttributeValue iav ON iav.ItemId = i.Id
	INNER JOIN CPQ_Attribute att ON iav.AttributeId = att.Id
	INNER JOIN CPQ_Line line ON line.Id = g.LineId
	INNER JOIN CPQ_LineAttributePurpose lap ON lap.AttributeId=att.Id AND lap.LineId=line.Id
	INNER JOIN @att_table ia ON ia.name = att.AttributeName AND CASE WHEN att.DataType = 'Number' THEN CASE WHEN CONVERT(decimal,iav.value)>=CONVERT(decimal,ia.value) THEN 1 ELSE 0 END WHEN att.DataType='Multi' AND ';'+iav.value+';' LIKE '%;'+ia.value+';%' THEN 1 WHEN ia.value=iav.value THEN 1 ELSE 0 END = 1
	WHERE lap.Active='TRUE' AND lap.PurposeId=4
	GROUP BY i.Id
	HAVING COUNT(*)=@att_count

	-- Get results
	SELECT DISTINCT 
	g.[Id] as GenericId
	,g.[LineId]
	,g.[GenericCode]
	,g.[GenericDescription]
	,g.[Rental_Term_Days]
	,g.[UOM_Intl]
	,g.[Rating_Intl]
	,g.[UOM_US]
	,g.[Rating_US]
	,g.[Rehire]
	,g.[Cable_Size_AWG]
	,g.[Cable_Size_mm]
	,g.[Amperage_Limit]
	,g.[Conductors]
	,g.[IsFuel]
	,g.[IsMeter]
	,g.[M3_Type]
	,g.[Cable_M3ItemNumber]
	,g.[CPQ_Sequence]
	,g.[VerCol]
	,g.[CableType]
	,g.[ShiftFactor]
	,g.[ConfigurationType]
	,g2.SubstitutionReason
	,a.[Id] as AssetId
	,a.[IndividualItemNumber]
	,a.[StatusCode]
	,a.[Warehouse]
	,a.[ShipAddress1]
	,a.[AgreementNumber]
	,a.[DeliveryDate]
	,a.[AgreementLineValidToDate]
	,a.[TerminationDate]
	,a.[CustomerName]
	,a.[TelemetryStatus]
	,a.[ServiceCenter]
	,a.[Description]
	,a.[Status]
	,a.[ManufacturerName]
	,a.[OwnerServiceCenter]
	,a.[CustomerNumber]
	,a.[ItemNumber]
	,a.[ShipAddress3]
	,a.[Facility]
	,a.[WarehouseLocation]
	,a.[Division]
	,h.CustomerName as ReservedCustomerName
	,h.CustomerNumber as ReserverdCustomerNumber
	,h.AgreementNumber as ReservedAgreement
	,l.DeliveryDate as ReservedDeliverydate
	,l.ValidFromDate as ReservedValidFromDate
	,l.ValidToDate as ReservedValidToDate
	,l.TerminationDate as ReservedTerminationDate
	,l.AgreementLineNumber as ReservedAgreementLineNumber
	,r.Notes as ReservationNotes
	,r.LineId as ReservedLineId
	,r.Id as ReservationId
	,w.Warehouse
	,g2.NumberNeeded
	,rf.Id as RingFenceId
	,rf.FromDate as RingFenceFromDate
	,rf.ToDate as RingFenceToDate
	,rf.Title as RingFenceNotes
	,a.EstimatedReadyDate
	,NoteCount=(SELECT COUNT(*) FROM Notes n WHERE n.ParentId = a.Id AND n.NoteType = 'asset')
	,r.IsConfirmed as ReservationConfirmed
	,a.[IONLastModified]
	FROM Assets a
	INNER JOIN @divs d ON d.code = a.Division
	INNER JOIN WarehouseItems w ON w.WarehouseCode = a.Warehouse AND NOT w.Warehouse like 'zz%'
	INNER JOIN CPQ_Item i ON a.ItemNumber = i.ItemNumber
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	INNER JOIN @generics g2 ON g2.Code = g.GenericCode
	INNER JOIN @matchingItems m ON m.ItemId = i.Id
	LEFT JOIN Reservations r ON r.AssetId = a.Id
	LEFT JOIN Lines l ON l.Id = r.LineId
	LEFT JOIN Headers h ON h.Id = l.HeaderId
	LEFT JOIN RingFenceItems rfi ON rfi.AssetId = a.Id
	LEFT JOIN Ringfences rf ON rf.Id = rfi.RingfenceId
	WHERE g.Active=1 AND g.Deleted=0
	AND (a.Status NOT IN ('RemovedStock', 'Scrap', 'Sold') OR r.LineId = @lineId)

	-- Related specific substitutions (bypass FAM attribute filter)
	UNION ALL
	SELECT DISTINCT 
	g.[Id] as GenericId
	,g.[LineId]
	,g.[GenericCode]
	,g.[GenericDescription]
	,g.[Rental_Term_Days]
	,g.[UOM_Intl]
	,g.[Rating_Intl]
	,g.[UOM_US]
	,g.[Rating_US]
	,g.[Rehire]
	,g.[Cable_Size_AWG]
	,g.[Cable_Size_mm]
	,g.[Amperage_Limit]
	,g.[Conductors]
	,g.[IsFuel]
	,g.[IsMeter]
	,g.[M3_Type]
	,g.[Cable_M3ItemNumber]
	,g.[CPQ_Sequence]
	,g.[VerCol]
	,g.[CableType]
	,g.[ShiftFactor]
	,g.[ConfigurationType]
	,'Related Substitute' as SubstitutionReason
	,a.[Id] as AssetId
	,a.[IndividualItemNumber]
	,a.[StatusCode]
	,a.[Warehouse]
	,a.[ShipAddress1]
	,a.[AgreementNumber]
	,a.[DeliveryDate]
	,a.[AgreementLineValidToDate]
	,a.[TerminationDate]
	,a.[CustomerName]
	,a.[TelemetryStatus]
	,a.[ServiceCenter]
	,a.[Description]
	,a.[Status]
	,a.[ManufacturerName]
	,a.[OwnerServiceCenter]
	,a.[CustomerNumber]
	,a.[ItemNumber]
	,a.[ShipAddress3]
	,a.[Facility]
	,a.[WarehouseLocation]
	,a.[Division]
	,h.CustomerName as ReservedCustomerName
	,h.CustomerNumber as ReserverdCustomerNumber
	,h.AgreementNumber as ReservedAgreement
	,l.DeliveryDate as ReservedDeliverydate
	,l.ValidFromDate as ReservedValidFromDate
	,l.ValidToDate as ReservedValidToDate
	,l.TerminationDate as ReservedTerminationDate
	,l.AgreementLineNumber as ReservedAgreementLineNumber
	,r.Notes as ReservationNotes
	,r.LineId as ReservedLineId
	,r.Id as ReservationId
	,w.Warehouse
	,rsi.NumberNeeded
	,rf.Id as RingFenceId
	,rf.FromDate as RingFenceFromDate
	,rf.ToDate as RingFenceToDate
	,rf.Title as RingFenceNotes
	,a.EstimatedReadyDate
	,NoteCount=(SELECT COUNT(*) FROM Notes n WHERE n.ParentId = a.Id AND n.NoteType = 'asset')
	,r.IsConfirmed as ReservationConfirmed
	,a.[IONLastModified]
	FROM Assets a
	INNER JOIN @divs d ON d.code = a.Division
	INNER JOIN WarehouseItems w ON w.WarehouseCode = a.Warehouse AND NOT w.Warehouse like 'zz%'
	INNER JOIN CPQ_Item i ON a.ItemNumber = i.ItemNumber
	INNER JOIN @relatedSpecificItems rsi ON rsi.ChildItemId = i.Id
	INNER JOIN @matchingItems m ON m.ItemId = i.Id
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	LEFT JOIN Reservations r ON r.AssetId = a.Id
	LEFT JOIN Lines l ON l.Id = r.LineId
	LEFT JOIN Headers h ON h.Id = l.HeaderId
	LEFT JOIN RingFenceItems rfi ON rfi.AssetId = a.Id
	LEFT JOIN Ringfences rf ON rf.Id = rfi.RingfenceId
	WHERE g.Active=1 AND g.Deleted=0
	AND (a.Status NOT IN ('RemovedStock', 'Scrap', 'Sold') OR r.LineId = @lineId)
END
ELSE
BEGIN

	-- Get results
	SELECT DISTINCT
	g.[Id] as GenericId
	,g.[LineId]
	,g.[GenericCode]
	,g.[GenericDescription]
	,g.[Rental_Term_Days]
	,g.[UOM_Intl]
	,g.[Rating_Intl]
	,g.[UOM_US]
	,g.[Rating_US]
	,g.[Rehire]
	,g.[Cable_Size_AWG]
	,g.[Cable_Size_mm]
	,g.[Amperage_Limit]
	,g.[Conductors]
	,g.[IsFuel]
	,g.[IsMeter]
	,g.[M3_Type]
	,g.[Cable_M3ItemNumber]
	,g.[CPQ_Sequence]
	,g.[VerCol]
	,g.[CableType]
	,g.[ShiftFactor]
	,g.[ConfigurationType]
	,g2.SubstitutionReason
	,a.[Id] as AssetId
	,a.[IndividualItemNumber]
	,a.[StatusCode]
	,a.[Warehouse]
	,a.[ShipAddress1]
	,a.[AgreementNumber]
	,a.[DeliveryDate]
	,a.[AgreementLineValidToDate]
	,a.[TerminationDate]
	,a.[CustomerName]
	,a.[TelemetryStatus]
	,a.[ServiceCenter]
	,a.[Description]
	,a.[Status]
	,a.[ManufacturerName]
	,a.[OwnerServiceCenter]
	,a.[CustomerNumber]
	,a.[ItemNumber]
	,a.[ShipAddress3]
	,a.[Facility]
	,a.[WarehouseLocation]
	,a.[Division]
	,h.CustomerName as ReservedCustomerName
	,h.CustomerNumber as ReserverdCustomerNumber
	,h.AgreementNumber as ReservedAgreement
	,l.DeliveryDate as ReservedDeliverydate
	,l.ValidFromDate as ReservedValidFromDate
	,l.ValidToDate as ReservedValidToDate
	,l.TerminationDate as ReservedTerminationDate
	,l.AgreementLineNumber as ReservedAgreementLineNumber
	,r.Notes as ReservationNotes
	,r.LineId as ReservedLineId
	,r.Id as ReservationId
	,w.Warehouse
	,g2.NumberNeeded
	,rf.Id as RingFenceId
	,rf.FromDate as RingFenceFromDate
	,rf.ToDate as RingFenceToDate
	,rf.Title as RingFenceNotes
	,a.EstimatedReadyDate
	,NoteCount=(SELECT COUNT(*) FROM Notes n WHERE n.ParentId = a.Id AND n.NoteType = 'asset')
	,r.IsConfirmed as ReservationConfirmed
	,a.[IONLastModified]
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
	AND (a.Status NOT IN ('RemovedStock', 'Scrap', 'Sold') OR r.LineId = @lineId)

	-- Related specific substitutions (bypass FAM attribute filter)
	UNION ALL
	SELECT DISTINCT
	g.[Id] as GenericId
	,g.[LineId]
	,g.[GenericCode]
	,g.[GenericDescription]
	,g.[Rental_Term_Days]
	,g.[UOM_Intl]
	,g.[Rating_Intl]
	,g.[UOM_US]
	,g.[Rating_US]
	,g.[Rehire]
	,g.[Cable_Size_AWG]
	,g.[Cable_Size_mm]
	,g.[Amperage_Limit]
	,g.[Conductors]
	,g.[IsFuel]
	,g.[IsMeter]
	,g.[M3_Type]
	,g.[Cable_M3ItemNumber]
	,g.[CPQ_Sequence]
	,g.[VerCol]
	,g.[CableType]
	,g.[ShiftFactor]
	,g.[ConfigurationType]
	,'Related Substitute' as SubstitutionReason
	,a.[Id] as AssetId
	,a.[IndividualItemNumber]
	,a.[StatusCode]
	,a.[Warehouse]
	,a.[ShipAddress1]
	,a.[AgreementNumber]
	,a.[DeliveryDate]
	,a.[AgreementLineValidToDate]
	,a.[TerminationDate]
	,a.[CustomerName]
	,a.[TelemetryStatus]
	,a.[ServiceCenter]
	,a.[Description]
	,a.[Status]
	,a.[ManufacturerName]
	,a.[OwnerServiceCenter]
	,a.[CustomerNumber]
	,a.[ItemNumber]
	,a.[ShipAddress3]
	,a.[Facility]
	,a.[WarehouseLocation]
	,a.[Division]
	,h.CustomerName as ReservedCustomerName
	,h.CustomerNumber as ReserverdCustomerNumber
	,h.AgreementNumber as ReservedAgreement
	,l.DeliveryDate as ReservedDeliverydate
	,l.ValidFromDate as ReservedValidFromDate
	,l.ValidToDate as ReservedValidToDate
	,l.TerminationDate as ReservedTerminationDate
	,l.AgreementLineNumber as ReservedAgreementLineNumber
	,r.Notes as ReservationNotes
	,r.LineId as ReservedLineId
	,r.Id as ReservationId
	,w.Warehouse
	,rsi.NumberNeeded
	,rf.Id as RingFenceId
	,rf.FromDate as RingFenceFromDate
	,rf.ToDate as RingFenceToDate
	,rf.Title as RingFenceNotes
	,a.EstimatedReadyDate
	,NoteCount=(SELECT COUNT(*) FROM Notes n WHERE n.ParentId = a.Id AND n.NoteType = 'asset')
	,r.IsConfirmed as ReservationConfirmed
	,a.[IONLastModified]
	FROM Assets a
	INNER JOIN @divs d ON d.code = a.Division
	INNER JOIN WarehouseItems w ON w.WarehouseCode = a.Warehouse AND NOT w.Warehouse like 'zz%'
	INNER JOIN CPQ_Item i ON a.ItemNumber = i.ItemNumber
	INNER JOIN @relatedSpecificItems rsi ON rsi.ChildItemId = i.Id
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	LEFT JOIN Reservations r ON r.AssetId = a.Id
	LEFT JOIN Lines l ON l.Id = r.LineId
	LEFT JOIN Headers h ON h.Id = l.HeaderId
	LEFT JOIN RingFenceItems rfi ON rfi.AssetId = a.Id
	LEFT JOIN Ringfences rf ON rf.Id = rfi.RingfenceId
	WHERE g.Active=1 AND g.Deleted=0
	AND (a.Status NOT IN ('RemovedStock', 'Scrap', 'Sold') OR r.LineId = @lineId)
END