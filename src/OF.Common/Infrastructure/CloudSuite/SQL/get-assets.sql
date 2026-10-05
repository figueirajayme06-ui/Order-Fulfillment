SELECT 
DISTINCT ml.SERN AS Id,
ml.SERN AS IndividualItemNumber,
ml.ITNO AS ItemNumber,
ml.CFI4 AS ServiceCenter,
ml.CFI4 AS OwnerServiceCenter,
ml.CFI5 as TelemetryStatus,
milmn.MVA0 as RunHours,
ma.SUNO AS ManufacturerName,
mm.ITDS AS Description,
mitl.whlo AS Warehouse,
mitl.whsl as WarehouseLocation,
mitl.whlo AS Container,
mitl.FACI AS Facility,
mitl.LMTS as IONLastModified,
mitl.BREM as Remark,
CASE 
    When mitl.whlo = mitw.WHLO AND mitl.whsl LIKE '%=>%' Then 'InTransit'
    WHEN mitl.whlo = mitw.WHLO Then 'InService'
    WHEN mitl.whsl = 'ASSESS' Then 'Assess'
    When mitl.whsl = 'PRESALSCRP' or (mitl.whsl = 'SOLD') Then 'Sold'
    When mitl.whsl = 'READY' or (mitl.whsl = 'RF-RDY') Then 'Available'
    When mitl.whsl = 'SCRAP' Then 'Scrap'
    When mitl.whsl in ('REPAIR','MAJRREPAIR') Then 'Repair'
    When mitl.whsl LIKE '%=>%' Then 'InTransit'
    When mitl.whsl = 'SERVICE' or (mitl.whsl = 'RF-SRV') Then 'InService'
    When mitl.whsl LIKE '%-ONH' Then 'OnHire'
    When mitl.whsl = 'RF-ONS' Then 'OnHire'
    When mitl.whlo = std.whoh AND al.ASTH = 40 AND al.LTYP = '6' Then 'Sold'
    When mitl.whlo = std.whoh AND al.ASTH = 40 Then 'Collection'
    When mitl.whlo = std.whoh Then 'OnHire' 
    When mitl.whsl = 'EVENTDB-GC' Then 'Available'
    When mitl.whsl IN ('YARD','QUARANTINE','O & T','IMPALA','EVENTDB') Then 'InService'
    When mitl.whsl = 'INTERNAL' Then 'OnHire'
    ELSE 'Available'
END AS Status,
CASE 
    When mitl.whsl NOT IN ('READY','SERVICE','ASSESS','REPAIR') Then mitl.whsl
    Else null
END AS UnknownLocation,
CASE
    When mitl.whlo = mitw.WHLO AND mitl.whsl LIKE '%=>%' Then 30
    WHEN mitl.whlo = mitw.WHLO Then 20
    WHEN mitl.whsl = 'ASSESS' Then 20  
    When mitl.whsl = 'PRESALSCRP' or (mitl.whsl = 'SOLD') Then 10 
    When mitl.whsl = 'READY' or (mitl.whsl = 'RF-RDY') or (mitl.whsl = 'RF-ONS') Then 10  
    When mitl.whsl = 'SCRAP' Then 10
    When mitl.whsl in ('REPAIR','MAJRREPAIR') Then 20
    When mitl.whsl LIKE '%=>%' Then 30
    When mitl.whsl = 'SERVICE' or (mitl.whsl = 'RF-SRV') Then 20
    When mitl.whsl LIKE '%-ONH' Then 50
    When mitl.whsl = 'RF-ONS' Then 50
    When mitl.whlo = std.whoh AND al.ASTH = 40 Then 40
    When mitl.whlo = std.whoh Then 50 
    When mitl.whsl = 'EVENTDB-GC' Then 10
    When mitl.whsl IN ('YARD','QUARANTINE','O & T','IMPALA','EVENTDB') Then 20
    When mitl.whsl = 'INTERNAL' Then 50
    ELSE 10
END AS StatusCode,
mitw2.DIVI as Division,
ah.SMCD as Saleperson,
al.AGNB AS AgreementNumber,
al.PONR AS CurrentLineNumber,
al.CUPL AS CustomerNumber,
al.SCNM AS CustomerName,
al.LVDT AS AgreementLineValidToDate,
al.FVDT AS AgreementLineValidFromDate,
al.TEDA AS TerminationDate,
al.DLDT AS DeliveryDate,
al.COLD AS CollectionDate,
al.SAD1 AS ShipAddress1,
al.SAD2 AS ShipAddress2,
al.SAD3 AS ShipAddress3,
al.SAD4 AS ShipAddress4,
'' as FuelNumber,
'' AS LotNumber,
'' as FuelNumberGALS
FROM MITLOC as mitl
LEFT JOIN (
select 
    ROW_NUMBER() OVER (PARTITION BY SERN ORDER BY SERN,LMTS DESC) AS row_num,
    STAT,
    ITNO,
    SERN,
    LMTS,
    CFI4,
    CFI5,
    OWTP,
    EQGR
from miloin
where SERN is not null
and EQGR = '101' --added to accomodate only looking at rental items from MILOIN, not Manufactured items
) ml
ON mitl.BANO = ml.SERN
AND ml.row_num = 1
LEFT JOIN miloma as ma
ON ma.BANO = ml.SERN and ma.ITNO = ml.ITNO
LEFT JOIN mitmas mm
ON mitl.itno = mm.ITNO
LEFT JOIN (
    SELECT 
        ROW_NUMBER() OVER (PARTITION BY BANO ORDER BY FVDT DESC, AGNB DESC, PONR DESC) AS row_num,
        AGNB, PONR, BANO, CUPL, SCNM, LVDT, FVDT, TEDA, DLDT, COLD,
        SAD1, SAD2, SAD3, SAD4, ASTH, LTYP
    FROM STAGLI
    WHERE BANO IS NOT NULL AND LTRIM(RTRIM(BANO)) != ''
    AND CONO = 1
) al
ON al.BANO = ml.SERN AND al.row_num = 1
LEFT JOIN mitwhl mitw
on mitl.whlo = mitw.WHLO and (mitw.WHLO like 'A5%')
LEFT JOIN mitwhl mitw2
on mitl.whlo = mitw2.WHLO
LEFT JOIN stdpot std
on mitl.whlo = std.whoh
LEFT JOIN SITCOM si
ON ml.ITNO = si.ITNO
LEFT JOIN STAGHE AH
ON AH.AGNB = ma.AAGN
LEFT JOIN milomn milmn
ON (ml.SERN = milmn.SERN AND ml.ITNO = milmn.ITNO AND milmn.MES0 = 'HOURS')
WHERE 
ma.CONO ='1'
AND ml.STAT != '99' --not deleted status
AND ml.SERN != 'WOITEM' --not a service item
AND mitl.cono = 1
AND mm.cono = 1
AND mitl.STQT = '1'
UNION ALL
SELECT
    ml.SERN AS Id,
    ml.SERN AS IndividualItemNumber,
    ml.ITNO AS ItemNumber,
    ml.CFI4 AS ServiceCenter,
    ml.CFI4 AS OwnerServiceCenter,
    ml.CFI5 as TelemetryStatus,
    milmn.MVA0 as RunHours,
    ma.SUNO AS ManufacturerName,
    mm.ITDS AS Description,
    mitl.whlo AS Warehouse,
    '' as WarehouseLocation,
    '' AS Container,
    '' AS Facility,
    mitl.LMTS as IONLastModified,
    '' as Remark,
    'RemovedStock' AS Status,
    '' AS UnknownLocation,
    90 AS StatusCode,
    cf.DIVI as Division,
    '' as Saleperson,
    '' AS AgreementNumber,
    0 AS CurrentLineNumber,
    '' AS CustomerNumber,
    '' AS CustomerName,
    000000 AS AgreementLineValidToDate,
    000000 AS AgreementLineValidFromDate,
    000000 AS TerminationDate,
    000000 AS DeliveryDate,
    000000 AS CollectionDate,
    '' AS ShipAddress1,
    '' AS ShipAddress2,
    '' AS ShipAddress3,
    '' AS ShipAddress4,
    '' as FuelNumber,
    '' AS LotNumber,
    '' as FuelNumberGALS
FROM (
    select 
        ROW_NUMBER() OVER (PARTITION BY SERN ORDER BY SERN,LMTS DESC) AS row_num,
        STAT,
        ITNO,
        SERN,
        LMTS,
        CFI4,
        CFI5,
        OWTP,
        EQGR
    from miloin
    where SERN is not null
    and EQGR = '101'
) ml
LEFT JOIN MITLOC mitl ON mitl.BANO = ml.SERN
LEFT JOIN miloma ma ON ma.BANO = ml.SERN and ma.ITNO = ml.ITNO
LEFT JOIN mitmas mm ON ml.ITNO = mm.ITNO
LEFT JOIN milomn milmn ON (ml.SERN = milmn.SERN AND ml.ITNO = milmn.ITNO AND milmn.MES0 = 'HOURS')
LEFT JOIN cfacil cf ON cf.FACI = ma.FACI
WHERE ml.row_num = 1
AND mitl.BANO IS NULL
AND ml.STAT != '99'
AND ml.SERN != 'WOITEM'
AND ma.CONO = '1'
AND mm.cono = 1