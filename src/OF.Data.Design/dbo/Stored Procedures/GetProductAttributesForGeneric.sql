CREATE PROCEDURE GetProductAttributesForGeneric
@genericId INT
AS

-- Performance improvement parameter sniffing
DECLARE @gid INT
SET @gid = @genericId

SELECT DISTINCT a.attributename as Name, iav.value as Value
FROM cpq_itemattributevalue av
INNER JOIN cpq_attribute a ON a.id=av.attributeid
INNER JOIN cpq_itemattributevalue iav ON iav.attributeId=a.id
INNER JOIN cpq_item i ON i.id=iav.itemid
INNER JOIN cpq_lineattributepurpose lap ON lap.attributeid=a.id
WHERE  lap.Active='TRUE' AND lap.PurposeId=4 AND i.genericId=@genericId
ORDER BY a.attributename, iav.value