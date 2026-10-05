MERGE INTO [dbo].[Assets] AS Target
USING [dbo].[ProductHierarchy_Staging] AS Source
ON Target.[IndividualItemNumber] = Source.[IndividualItemNumber]
WHEN MATCHED AND (
    -- Only update if hierarchy data actually changed
    ISNULL(Target.[ProductGroup], '') != ISNULL(Source.[ProductGroup], '') OR
    ISNULL(Target.[ProductCategory], '') != ISNULL(Source.[ProductCategory], '') OR
    ISNULL(Target.[InternationalSizeRating], '') != ISNULL(Source.[InternationalActualRatingKey], '') OR
    ISNULL(Target.[UsSizeRating], '') != ISNULL(Source.[UsActualRatingKey], '')
) THEN
    UPDATE SET
        Target.[ProductGroup] = Source.[ProductGroup],
        Target.[ProductCategory] = Source.[ProductCategory],
        Target.[InternationalSizeRating] = Source.[InternationalActualRatingKey],
        Target.[UsSizeRating] = Source.[UsActualRatingKey];