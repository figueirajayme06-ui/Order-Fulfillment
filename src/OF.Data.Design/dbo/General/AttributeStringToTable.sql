-- =============================================
-- Function: AttributeStringToTable
-- Description: Parses semicolon-delimited attribute string into table format
-- Author: CPQ Development Team
-- Created: 2026-02-05
-- Last Modified: 2026-05-15
-- =============================================
CREATE FUNCTION [dbo].[AttributeStringToTable]
(
    @attributes NVARCHAR(max)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        LTRIM(RTRIM(SUBSTRING(value, 1, CHARINDEX(':', value) - 1)))            [Name],
        LTRIM(RTRIM(SUBSTRING(value, CHARINDEX(':', value) + 1, LEN(value))))   [Value],
        ca.Id,
        ca.DataType
    FROM STRING_SPLIT(@attributes, ';')
    INNER JOIN CPQ_Attribute ca ON ca.CPQAttribute = LTRIM(RTRIM(SUBSTRING(value, 1, CHARINDEX(':', value) - 1)))
    WHERE RTRIM(value) <> ''
        AND CHARINDEX(':', value) > 0
        AND ca.CPQAttribute != 'SBQQ__Quantity__c'
        AND ca.CPQAttribute != 'Request_for_Technical_Review__c'
        AND ca.CPQAttribute != 'Voltage @ 50Hz'
        AND ca.CPQAttribute != 'Voltage @ 60Hz'
        AND ca.CPQAttribute != 'Shift_factor__c'
        AND ca.CPQAttribute != 'Frequency__c'
        AND ca.CPQAttribute != 'RunningCycle__c'
        AND ca.CPQAttribute != 'attributes' -- Exclude the nested 'attributes' object that sometimes appears
        AND ca.CPQAttribute NOT LIKE 'comments%'
)
GO
