CREATE VIEW vwAssetItems
AS
	SELECT DISTINCT
	a.Id,
	a.Description,
	a.ItemNumber,
	a.Division,
	a.Warehouse,
	a.Status,
	a.AgreementNumber,
	a.DeliveryDate,
	a.AgreementLineValidToDate as LineValidTo,
	a.AgreementLineValidFromDate as LineValidFrom,
	a.TerminationDate,
	a.CollectionDate,
	a.CustomerNumber,
	a.CustomerName,
	a.Facility,
	a.WarehouseLocation,
	ISNULL(w.Warehouse, '') as WarehouseName,
	a.ProductGroup,
	a.ProductCategory,
	a.RunHours,
	a.EstimatedReadyDate,
	a.USSizeRating as Size,
	a.TelemetryStatus,
	a.Remark,
	f.Id as Family,
	l.Id as Line,
	g.id as Generic,
	CONVERT(varchar, a.Id) as BarData,
	CASE WHEN ri.Id IS NULL THEN 0 ELSE 1 END + CASE WHEN EXISTS(SELECT * FROM Notes WHERE ParentId=a.Id AND NoteType='asset') THEN 20 ELSE 10 END AS StatusInDateRange,
	CASE
		WHEN a.Status = 'ONHIRE' THEN 0
		WHEN COALESCE(a.CollectionDate, a.TerminationDate, a.AgreementLineValidToDate) IS NULL THEN NULL
		WHEN COALESCE(a.CollectionDate, a.TerminationDate, a.AgreementLineValidToDate) > CAST(GETUTCDATE() AS DATE) THEN NULL
		ELSE DATEDIFF(DAY, COALESCE(a.CollectionDate, a.TerminationDate, a.AgreementLineValidToDate), CAST(GETUTCDATE() AS DATE))
	END AS DaysOffHire
	FROM Assets a
	LEFT OUTER JOIN WarehouseItems w ON w.WarehouseCode = a.Warehouse
	INNER JOIN CPQ_Item i ON a.ItemNumber = i.ItemNumber
	INNER JOIN CPQ_Generic g ON g.Id = i.GenericId
	INNER JOIN CPQ_Line l on l.Id = g.LineId
	INNER JOIN CPQ_Family f ON f.Id = l.FamilyId
	LEFT JOIN RingFenceItems ri ON ri.AssetId = a.Id
	WHERE g.Active=1 AND g.Deleted=0
