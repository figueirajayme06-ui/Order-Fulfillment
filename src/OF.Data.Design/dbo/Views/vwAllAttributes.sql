CREATE VIEW vwAllAttributes
AS
SELECT DISTINCT a. * FROM CPQ_Attribute a 
INNER JOIN CPQ_LineAttributePurpose lap ON lap.AttributeId = a.Id 
INNER JOIN CPQ_Purpose p ON p.Id = lap.PurposeId 
WHERE p.PurposeDescription='Master'

UNION ALL

SELECT DISTINCT a.Id, al.Translation as AttributeDescription, a.CPQAttribute, a.DataType, a.ValidationLength, a.VerCol, al.Translation as AttributeName
FROM CPQ_Attribute a 
INNER JOIN CPQ_LineAttributePurpose lap ON lap.AttributeId = a.Id 
INNER JOIN CPQ_Purpose p ON p.Id = lap.PurposeId 
INNER JOIN AttributeLanguage al ON al.LookupKey = a.CPQAttribute
WHERE p.PurposeDescription='Master'