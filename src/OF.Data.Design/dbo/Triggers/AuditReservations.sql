CREATE TRIGGER AuditReservations
ON Reservations
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT * FROM inserted) AND EXISTS (SELECT * FROM deleted)
    BEGIN
        -- UPDATE: one audit row per updated row, handling bulk statements correctly
        INSERT INTO AuditLog (TableName, OperationType, NewValues, UpdateDate, UpdatedBy, RecordID)
        SELECT
            'Reservations',
            'U',
            (SELECT * FROM inserted i2 WHERE i2.ID = i.ID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            GETDATE(),
            SYSTEM_USER,
            i.ID
        FROM inserted i;
    END
    ELSE IF EXISTS (SELECT * FROM inserted)
    BEGIN
        -- INSERT: one audit row per inserted row, handling bulk statements correctly
        INSERT INTO AuditLog (TableName, OperationType, NewValues, UpdateDate, UpdatedBy, RecordID)
        SELECT
            'Reservations',
            'I',
            (SELECT * FROM inserted i2 WHERE i2.ID = i.ID FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
            GETDATE(),
            SYSTEM_USER,
            i.ID
        FROM inserted i;
    END
    ELSE IF EXISTS (SELECT * FROM deleted)
    BEGIN
        -- DELETE: one audit row per deleted row (no new values to capture)
        INSERT INTO AuditLog (TableName, OperationType, NewValues, UpdateDate, UpdatedBy, RecordID)
        SELECT
            'Reservations',
            'D',
            NULL,
            GETDATE(),
            SYSTEM_USER,
            d.ID
        FROM deleted d;
    END
END
