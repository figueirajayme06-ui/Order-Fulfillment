SELECT 
	Id,
	M3_Line_Number_Id__c,
	SBQQ__QuoteLine__c,
	SBQQ__QuoteLine__r.SBQQ__Number__c,
	SBQQ__QuoteLine__r.Rich_Displayed_In_Line_Items_With_Attrib__c,
    SBQQ__QuoteLine__r.SBQQ__Description__c,
	SBQQ__QuoteLine__r.PS_AG_Selected_Attributes_As_Text_EN__c,
	SBQQ__QuoteLine__r.Selected_Attributes_As_Text__c,
	SBQQ__QuoteLine__r.Generic_Code__c,
	SBQQ__QuoteLine__r.SBQQ__Quote__c,
	SBQQ__QuoteLine__r.SBQQ__Quote__r.Name,
	SBQQ__QuoteLine__r.SBQQ__Group__r.Name
FROM OrderItem 
WHERE Id = '{0}'