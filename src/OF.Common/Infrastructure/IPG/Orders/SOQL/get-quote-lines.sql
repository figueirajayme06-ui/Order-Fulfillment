SELECT
    Id,
    LastModifiedDate,
    SBQQ__Number__c,
    M3_Line_Type__c,
    SBQQ__ProductCode__c,
    SBQQ__Quantity__c,
    SBQQ__Group__r.Name,
	Rich_Displayed_In_Line_Items_With_Attrib__c,
    SBQQ__Description__c,
    PS_AG_Selected_Attributes_As_Text_EN__c,
    Selected_Attributes_As_Text__c,
    On_Hire_Date__c,
    Off_Hire_Date__c,
    NUMBER_OF_SHIFTS__C,
	Proposal_Section__c
FROM SBQQ__QuoteLine__c
WHERE SBQQ__Quote__c = '{0}' {1}