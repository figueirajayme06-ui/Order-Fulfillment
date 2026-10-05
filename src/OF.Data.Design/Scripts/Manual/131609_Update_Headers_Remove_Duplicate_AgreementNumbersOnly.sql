WITH Duplicates AS (
    SELECT 
        AgreementNumbersOnly,
        ROW_NUMBER() OVER (PARTITION BY AgreementNumbersOnly ORDER BY Id DESC) AS row_num,
        Id
    FROM dbo.Headers
    WHERE AgreementNumbersOnly IS NOT NULL
)
UPDATE h
SET AgreementNumbersOnly = NULL
FROM dbo.Headers h
INNER JOIN Duplicates d ON h.Id = d.Id
WHERE d.row_num > 1;