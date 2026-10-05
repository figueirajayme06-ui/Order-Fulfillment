-- Disable CDC before the DACPAC publish (run by .azd/actions/deploy-sql.yml).
--
-- SqlPackage fails PLAN ANALYSIS with SQL72035 if it must ALTER a CDC-tracked table, and that
-- happens before any in-DACPAC pre-deploy script could run - so CDC must be disabled here, before
-- SqlPackage runs at all. This snapshots the exact CDC configuration into dbo._cdc_restore so the
-- companion restore-cdc.sql can faithfully re-enable it after the publish.
--
-- Executed as a single batch via System.Data.SqlClient (no GO / no :r).
SET NOCOUNT ON;
IF DB_NAME() = 'master' THROW 50000, 'Refusing to modify CDC on master database.', 1;

-- No-op where CDC is not enabled (keeps this safe on non-CDC environments; the cdc.* objects only
-- exist when CDC is enabled on the database).
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_cdc_enabled = 1)
BEGIN
    PRINT 'CDC is not enabled on this database - nothing to disable.';
    RETURN;
END

-- Fail fast (before disabling anything) if we lack the rights to manage CDC.
IF (ISNULL(IS_ROLEMEMBER('db_owner'), 0) = 0 AND HAS_PERMS_BY_NAME(NULL, NULL, 'CONTROL') = 0)
    THROW 50010, 'Deployment principal lacks db_owner/CONTROL; cannot manage CDC.', 1;

-- Preserve a snapshot left by a previous failed run; only capture a fresh one if none exists
-- (overwriting could erase the original config if CDC was already disabled by a prior failure).
IF OBJECT_ID('dbo._cdc_restore') IS NULL
BEGIN
    SELECT
        SCHEMA_NAME(t.schema_id)            AS source_schema,
        t.name                              AS source_table,
        ct.capture_instance                 AS capture_instance,
        ct.role_name                        AS role_name,
        ct.supports_net_changes             AS supports_net_changes,
        ct.index_name                       AS index_name,
        ct.filegroup_name                   AS filegroup_name,
        STUFF((
            SELECT ',' + QUOTENAME(cc.column_name)
            FROM cdc.captured_columns cc
            WHERE cc.object_id = ct.object_id
            ORDER BY cc.column_ordinal
            FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 1, '') AS captured_columns
    INTO dbo._cdc_restore
    FROM cdc.change_tables ct
    JOIN sys.tables t ON ct.source_object_id = t.object_id;
END

-- Disable CDC on every tracked source table (all capture instances).
DECLARE @schema sysname, @table sysname;
DECLARE dis CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT source_schema, source_table FROM dbo._cdc_restore;
OPEN dis;
FETCH NEXT FROM dis INTO @schema, @table;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM cdc.change_tables ct
               JOIN sys.tables t ON ct.source_object_id = t.object_id
               WHERE SCHEMA_NAME(t.schema_id) = @schema AND t.name = @table)
    BEGIN
        PRINT CONCAT('Disabling CDC on [', @schema, '].[', @table, ']');
        EXEC sys.sp_cdc_disable_table
            @source_schema   = @schema,
            @source_name     = @table,
            @capture_instance = 'all';
    END
    FETCH NEXT FROM dis INTO @schema, @table;
END
CLOSE dis;
DEALLOCATE dis;
