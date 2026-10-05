-- Order Fulfilment substitution policy.
--
-- Unlike CPQ Next, the fulfilment workbench presents every configured generic
-- substitution purpose. The planner enters the quantity required for a
-- substitute; this function deliberately does not apply a conversion ratio.
CREATE FUNCTION [dbo].[GetFulfilmentGenericsWithSubstitutions]
(
    @genericCode NVARCHAR(255)
)
RETURNS TABLE
AS
RETURN
(
    WITH FilteredGenerics AS
    (
        SELECT g.Id, g.GenericCode, g.GenericDescription
        FROM dbo.CPQ_Generic g
        WHERE g.Active = 1
            AND g.Deleted = 0
            AND (g.Rehire IS NULL OR g.Rehire = 'No')
            AND g.GenericCode NOT LIKE 'XXMISC%'
            AND g.GenericCode = @genericCode
    )
    SELECT Id, GenericCode, GenericDescription
    FROM FilteredGenerics

    UNION

    SELECT DISTINCT child.Id, child.GenericCode, child.GenericDescription
    FROM dbo.CPQ_GenericSubstitution substitution
    INNER JOIN FilteredGenerics parent ON parent.Id = substitution.ParentGenericId
    INNER JOIN dbo.CPQ_Generic child ON child.Id = substitution.ChildGenericId
    WHERE substitution.ParentGenericId <> substitution.ChildGenericId
)
