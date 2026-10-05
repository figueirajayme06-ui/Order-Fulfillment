CREATE TABLE AuditLog_Staging (
    AuditLogID INT IDENTITY(1,1),
    TableName VARCHAR(128) NOT NULL,
    OperationType CHAR(1) NOT NULL,
    NewValues NVARCHAR(MAX),
    UpdateDate DATETIME NOT NULL,
    UpdatedBy VARCHAR(128) NOT NULL, 
    [RecordId] INT NOT NULL,
    CONSTRAINT PK_AuditLogStaging PRIMARY KEY (TableName, AuditLogID)
) ON AuditLogPS(TableName);