-- CPQ Next substitution compatibility contract.
--
-- Keep this function limited to Substitute Up relationships. Order Fulfilment
-- uses dbo.GetFulfilmentGenericsWithSubstitutions for its broader planner flow.
CREATE FUNCTION [dbo].[GetGenericsWithSubstitutions]
(
	@familyId		INT = NULL,
	@lineId			INT,
	@genericCode	NVARCHAR(255) = NULL,
	@region NVARCHAR(255) = NULL,
	@descriptionSearch	NVARCHAR(500) = NULL
)
RETURNS TABLE 
AS
RETURN 
(
 	WITH FilteredGenerics AS
	(
		SELECT DISTINCT g.Id, g.GenericCode, g.GenericDescription 
		FROM CPQ_Generic g
		INNER JOIN CPQ_Line l ON l.Id = g.LineId
		INNER JOIN CPQ_Family f ON f.Id = l.FamilyId
		LEFT JOIN CPQ_RegionGeneric rg ON rg.GenericId = g.Id
		LEFT JOIN CPQ_Region r ON r.Id = rg.RegionId
		WHERE 
			g.Active = 1 
			AND g.Deleted = 0
			AND (@familyId IS NULL OR f.Id = @familyId)
			AND (@lineId IS NULL OR g.LineId = @lineId)
			AND (g.Rehire IS NULL OR g.Rehire = 'No')
			AND g.GenericCode NOT LIKE 'XXMISC%'
			AND (
					@descriptionSearch IS NULL 
					OR LEN (@descriptionSearch) = 0 
					OR g.GenericDescription LIKE '%' + @descriptionSearch + '%' 
				)
			AND (
					@region IS NULL 
					OR (@region = 'NAM' AND r.RegionOrgCode = 'NAMERICA')
					OR (@region <> 'NAM' AND r.RegionOrgCode <> 'NAMERICA')
				)
			AND (
					@genericCode IS NULL 
					OR LEN(@genericCode) = 0 
					OR g.GenericCode = @genericCode
				)
	)
	SELECT Id, GenericCode, GenericDescription FROM FilteredGenerics

	UNION

	SELECT DISTINCT g2.Id, g2.GenericCode, g2.GenericDescription 
	FROM CPQ_GenericSubstitution s
	INNER JOIN FilteredGenerics g ON g.Id = s.ParentGenericId
	INNER JOIN CPQ_Generic g2 ON g2.Id = s.ChildGenericId
	WHERE s.Purpose = 'Substitute Up'
		AND s.ParentGenericId <> s.ChildGenericId
		AND (
				@descriptionSearch IS NULL 
				OR LEN (@descriptionSearch) = 0 
				OR g2.GenericDescription LIKE '%' + @descriptionSearch + '%' 
			)
)
