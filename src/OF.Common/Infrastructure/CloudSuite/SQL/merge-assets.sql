MERGE INTO dbo.Assets AS target
USING dbo.AssetsStaging AS source
ON target.Id = source.Id
WHEN MATCHED AND (
    -- Only update if something actually changed
    target.IndividualItemNumber != source.IndividualItemNumber OR
    ISNULL(target.StatusCode, '') != ISNULL(source.StatusCode, '') OR
    ISNULL(target.Warehouse, '') != ISNULL(source.Warehouse, '') OR
    ISNULL(target.ShipAddress1, '') != ISNULL(source.ShipAddress1, '') OR
    ISNULL(target.AgreementNumber, '') != ISNULL(source.AgreementNumber, '') OR
    ISNULL(target.DeliveryDate, '1900-01-01') != ISNULL(source.DeliveryDate, '1900-01-01') OR
    ISNULL(target.AgreementLineValidToDate, '1900-01-01') != ISNULL(source.AgreementLineValidToDate, '1900-01-01') OR
    ISNULL(target.TerminationDate, '1900-01-01') != ISNULL(source.TerminationDate, '1900-01-01') OR
    ISNULL(target.CustomerName, '') != ISNULL(source.CustomerName, '') OR
    ISNULL(target.TelemetryStatus, '') != ISNULL(source.TelemetryStatus, '') OR
    ISNULL(target.ServiceCenter, '') != ISNULL(source.ServiceCenter, '') OR
    ISNULL(target.Description, '') != ISNULL(source.Description, '') OR
    ISNULL(target.Status, '') != ISNULL(source.Status, '') OR
    ISNULL(target.ManufacturerName, '') != ISNULL(source.ManufacturerName, '') OR
    ISNULL(target.OwnerServiceCenter, '') != ISNULL(source.OwnerServiceCenter, '') OR
    ISNULL(target.CustomerNumber, '') != ISNULL(source.CustomerNumber, '') OR
    ISNULL(target.ItemNumber, '') != ISNULL(source.ItemNumber, '') OR
    ISNULL(target.ShipAddress3, '') != ISNULL(source.ShipAddress3, '') OR
    ISNULL(target.Facility, '') != ISNULL(source.Facility, '') OR
    ISNULL(target.IONLastModified, '1900-01-01') != ISNULL(source.IONLastModified, '1900-01-01') OR
    ISNULL(target.WarehouseLocation, '') != ISNULL(source.WarehouseLocation, '') OR
    ISNULL(target.Division, '') != ISNULL(source.Division, '') OR
    ISNULL(target.Remark, '') != ISNULL(source.Remark, '') OR
    ISNULL(target.RunHours, 0) != ISNULL(source.RunHours, 0) OR
    ISNULL(target.AgreementLineValidFromDate, '1900-01-01') != ISNULL(source.AgreementLineValidFromDate, '1900-01-01') OR
    ISNULL(target.CollectionDate, '1900-01-01') != ISNULL(source.CollectionDate, '1900-01-01')
) THEN
    UPDATE SET
        target.IndividualItemNumber = source.IndividualItemNumber,
        target.StatusCode = source.StatusCode,
        target.Warehouse = source.Warehouse,
        target.ShipAddress1 = source.ShipAddress1,
        target.AgreementNumber = source.AgreementNumber,
        target.DeliveryDate = source.DeliveryDate,
        target.AgreementLineValidToDate = source.AgreementLineValidToDate,
        target.TerminationDate = source.TerminationDate,
        target.CustomerName = source.CustomerName,
        target.TelemetryStatus = source.TelemetryStatus,
        target.ServiceCenter = source.ServiceCenter,
        target.Description = source.Description,
        target.Status = source.Status,
        target.ManufacturerName = source.ManufacturerName,
        target.OwnerServiceCenter = source.OwnerServiceCenter,
        target.CustomerNumber = source.CustomerNumber,
        target.ItemNumber = source.ItemNumber,
        target.ShipAddress3 = source.ShipAddress3,
        target.Facility = source.Facility,
        target.IONLastModified = source.IONLastModified,
        target.WarehouseLocation = source.WarehouseLocation,
        target.Division = source.Division,
        target.Remark = source.Remark,
        target.RunHours = source.RunHours,
        target.AgreementLineValidFromDate = source.AgreementLineValidFromDate,
        target.CollectionDate = source.CollectionDate

WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, IndividualItemNumber, StatusCode, Warehouse, ShipAddress1, AgreementNumber, DeliveryDate, 
            AgreementLineValidToDate, TerminationDate, CustomerName, TelemetryStatus, ServiceCenter, Description, 
            Status, ManufacturerName, OwnerServiceCenter, CustomerNumber, ItemNumber, ShipAddress3, Facility, 
            IONLastModified, WarehouseLocation, Division, Remark, ProductGroup, ProductCategory, 
            InternationalSizeRating, UsSizeRating, RunHours, EstimatedReadyDate, AgreementLineValidFromDate, 
            CollectionDate)
    VALUES (source.Id, source.IndividualItemNumber, source.StatusCode, source.Warehouse, source.ShipAddress1, 
            source.AgreementNumber, source.DeliveryDate, source.AgreementLineValidToDate, source.TerminationDate, 
            source.CustomerName, source.TelemetryStatus, source.ServiceCenter, source.Description, source.Status, 
            source.ManufacturerName, source.OwnerServiceCenter, source.CustomerNumber, source.ItemNumber, 
            source.ShipAddress3, source.Facility, source.IONLastModified, source.WarehouseLocation, source.Division, 
            source.Remark, source.ProductGroup, source.ProductCategory, source.InternationalSizeRating, 
            source.UsSizeRating, source.RunHours, source.EstimatedReadyDate, source.AgreementLineValidFromDate, 
            source.CollectionDate)
OUTPUT $action, inserted.Id, deleted.Id;
