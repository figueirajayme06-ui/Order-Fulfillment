CREATE VIEW [dbo].[vwHeaders]
AS
SELECT h.[Id]
      ,COALESCE(h.[AgreementNumber], h.[QuotePublicId]) as AgreementNumber
      ,h.[OnHireDate]
      ,h.[OffHireDate]
      ,h.[Status]
      ,h.[CustomerName]
      ,h.[CustomerAddress]
      ,h.[CustomerNumber]
      ,h.[Division]
      ,h.[CustomerAddressCode]
      ,h.[OrderSource]
      ,h.[ChangeSequence]
      ,h.[IsDeleted]
      ,h.[FulfilmentStatus]
      ,h.[LastUpdatedBy]
      ,h.[LastUpdatedDate]
      ,h.[Probability]
	  ,h.[OpportunityName]
	  ,h.[OpportunityStage]
	  ,lc.DeliveryDate
	  ,lc.ValidFromDate
	  ,lc.ValidToDate
	  ,lc.TerminationDate
	  ,lc.CollectionDate
	  ,lc.FromDate
	  ,lc.FromDateWithDelivery
	  ,lc.ToDate
	  ,lc.ToDateWithCollection
	  ,lc.LineCount
	  ,lc.MinFulfilmentStatus
	  ,lc.MaxFulfilmentStatus
	  ,COALESCE(h.RentalDepot, lc.Warehouse) as Warehouse
	  ,u.FullName as LastUpdatedByName
	  ,h.AgreementNumber + '|' + CONVERT(varchar, lc.FromDate, 126) + '|' +  CONVERT(varchar, lc.toDate, 126) + '|' + CONVERT(varchar,h.FulfilmentStatus) + '|' + CONVERT(varchar, lc.FromDateWithDelivery, 126) + '|' + CONVERT(varchar, lc.ToDateWithCollection, 126) as BarData
  FROM [Headers] h
  INNER JOIN
  (
	  SELECT
	  l.HeaderId,
	  COUNT(*) as LineCount,
	  MIN(l.DeliveryDate) as DeliveryDate,
	  MIN(l.ValidFromDate) as ValidFromDate,
	  MAX(l.ValidToDate) as ValidToDate,
	  MIN(l.ValidFromDate) as FromDate,
	  MIN(COALESCE(l.DeliveryDate, l.ValidFromDate)) as FromDateWithDelivery,
	  MAX(COALESCE(l.TerminationDate, l.ValidToDate)) as ToDate,
	  MAX(COALESCE(l.CollectionDate, l.TerminationDate, l.ValidToDate)) as ToDateWithCollection,
	  MAX(l.TerminationDate) as TerminationDate,
	  MAX(l.CollectionDate) as CollectionDate,
	  MIN(l.FulfilmentStatus) as MinFulfilmentStatus,
	  MAX(l.FulfilmentStatus) as MaxFulfilmentStatus,
	  MIN(l.Warehouse) as Warehouse
	  FROM Lines l
	  LEFT JOIN CPQ_Service s ON s.ProductCode=l.ItemNumber
	  WHERE l.IsDeleted=0 AND l.RequiresFulfilment=1 AND s.Id IS NULL
	  GROUP BY l.HeaderId
  ) lc ON h.Id = lc.HeaderId
  LEFT JOIN Users u ON u.LoginName = h.LastUpdatedBy
  WHERE FromDate IS NOT NULL AND ToDate IS NOT NULL AND lc.LineCount > 0 AND (h.AgreementNumber IS NOT NULL OR h.QuotePublicId IS NOT NULL)
GO