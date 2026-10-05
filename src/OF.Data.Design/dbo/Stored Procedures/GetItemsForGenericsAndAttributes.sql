CREATE PROCEDURE GetItemsForGenericAndAttributes
@genericId INT,
@attributes NVARCHAR(1000),
@division NVARCHAR(3)
AS

DECLARE @gid INT
SET @gid = @genericId

-- Parse attributes
DECLARE @att_table TABLE(name NVARCHAR(200), value NVARCHAR(200))
INSERT @att_table(name, value)
SELECT SUBSTRING(value,1,CHARINDEX(':',value)-1),SUBSTRING(value,CHARINDEX(':', value)+1,LEN(value))
FROM STRING_SPLIT(@attributes,';')
WHERE RTRIM(value) <> '';

DECLARE @matchingItems TABLE(ItemID INT)
DECLARE @att_count INT
SELECT @att_count=COUNT(*) FROM @att_table

IF @att_count > 0
BEGIN
	INSERT @matchingItems(ItemID)
	SELECT i.Id
	FROM CPQ_Generic g
	INNER JOIN CPQ_Item i ON i.GenericId = g.Id
	INNER JOIN CPQ_ItemAttributeValue iav ON iav.ItemId = i.Id
	INNER JOIN CPQ_Attribute att ON iav.AttributeId = att.Id
	INNER JOIN CPQ_Line line ON line.Id = g.LineId
	INNER JOIN CPQ_LineAttributePurpose lap ON lap.AttributeId=att.Id AND lap.LineId=line.Id
	INNER JOIN @att_table ia ON ia.name = att.AttributeName AND CASE WHEN att.DataType = 'Number' THEN CASE WHEN CONVERT(decimal,iav.value)>=CONVERT(decimal,ia.value) THEN 1 ELSE 0 END WHEN att.DataType='Multi' AND ';'+iav.value+';' LIKE '%;'+ia.value+';%' THEN 1 WHEN ia.value=iav.value THEN 1 ELSE 0 END = 1
	WHERE g.Id = @gid AND lap.Active='TRUE' AND lap.PurposeId=4 -- FAM attribute
	GROUP BY i.Id
	HAVING COUNT(*)=@att_count
END
ELSE
BEGIN
	INSERT @matchingItems(ItemID)
	SELECT i.Id
	FROM CPQ_Generic g
	INNER JOIN CPQ_Item i ON i.GenericId = g.Id
	WHERE g.Id = @gid
END

SELECT DISTINCT i.*
FROM cpq_item i
INNER JOIN @matchingItems m ON m.ItemID = i.Id
LEFT JOIN Assets a ON a.itemnumber=i.itemnumber
LEFT JOIN ProductItems p ON p.itemnumber=i.itemnumber
WHERE i.Deleted=0 AND (p.division=@division OR a.division=@division)
ORDER BY i.ItemNumber
