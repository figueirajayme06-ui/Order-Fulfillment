-- Restore CDC after the DACPAC publish (run by .azd/actions/deploy-sql.yml).
--
-- Re-enables CDC faithfully from the snapshot captured by disable-cdc.sql (dbo._cdc_restore),
-- preserving capture-instance names, role, net-changes, index and captured columns per environment.
-- Runs on always() in the pipeline, so CDC is restored even if the publish failed.
--
-- Executed as a single batch via System.Data.SqlClient (no GO / no :r).
SET NOCOUNT ON;
IF OBJECT_ID('dbo._cdc_restore') IS NULL
BEGIN
    PRINT 'No CDC snapshot found - nothing to restore.';
    RETURN;
END

-- If the snapshot is empty (CDC was not enabled / no tracked tables), just clean up and exit.
-- This prevents accidentally enabling CDC on a database that never had it.
IF NOT EXISTS (SELECT 1 FROM dbo._cdc_restore)
BEGIN
    DROP TABLE dbo._cdc_restore;
    PRINT 'CDC snapshot is empty - nothing to restore.';
    RETURN;
END

-- Ensure CDC is enabled at the database level before enabling tables.
IF EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_cdc_enabled = 0)
    EXEC sys.sp_cdc_enable_db;

DECLARE @schema sysname, @table sysname, @inst sysname, @role sysname,
        @net bit, @idx sysname, @fg sysname, @cols nvarchar(max);
DECLARE @failures nvarchar(max) = N'';

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT source_schema, source_table, capture_instance, role_name,
           supports_net_changes, index_name, filegroup_name, captured_columns
    FROM dbo._cdc_restore;

OPEN cur;
FETCH NEXT FROM cur INTO @schema, @table, @inst, @role, @net, @idx, @fg, @cols;
WHILE @@FETCH_STATUS = 0
BEGIN
    -- Only (re)create this capture instance if it is not already present (handles a table that has
    -- two capture instances).
    IF NOT EXISTS (SELECT 1 FROM cdc.change_tables ct
                   JOIN sys.tables t ON ct.source_object_id = t.object_id
                   WHERE SCHEMA_NAME(t.schema_id) = @schema AND t.name = @table
                     AND ct.capture_instance = @inst)
    BEGIN
        BEGIN TRY
            -- Tier 1: faithful restore (role, net-changes index, filegroup, captured columns).
            EXEC sys.sp_cdc_enable_table
                @source_schema = @schema, @source_name = @table, @capture_instance = @inst,
                @role_name = @role, @supports_net_changes = @net, @index_name = @idx,
                @filegroup_name = @fg, @captured_column_list = @cols;
            PRINT CONCAT('Restored CDC (full) on [', @schema, '].[', @table, '] / ', @inst);
        END TRY
        BEGIN CATCH
            BEGIN TRY
                -- Tier 2: capture all current columns (the saved column list may be stale after a
                -- schema change).
                IF NOT EXISTS (SELECT 1 FROM cdc.change_tables ct
                               JOIN sys.tables t ON ct.source_object_id = t.object_id
                               WHERE SCHEMA_NAME(t.schema_id) = @schema AND t.name = @table
                                 AND ct.capture_instance = @inst)
                    EXEC sys.sp_cdc_enable_table
                        @source_schema = @schema, @source_name = @table, @capture_instance = @inst,
                        @role_name = @role, @supports_net_changes = @net, @index_name = @idx,
                        @filegroup_name = @fg;
                PRINT CONCAT('Restored CDC (all columns) on [', @schema, '].[', @table, '] / ', @inst);
            END TRY
            BEGIN CATCH
                BEGIN TRY
                    -- Tier 3: minimal restore to guarantee CDC is back on (no net-changes/index).
                    IF NOT EXISTS (SELECT 1 FROM cdc.change_tables ct
                                   JOIN sys.tables t ON ct.source_object_id = t.object_id
                                   WHERE SCHEMA_NAME(t.schema_id) = @schema AND t.name = @table
                                     AND ct.capture_instance = @inst)
                        EXEC sys.sp_cdc_enable_table
                            @source_schema = @schema, @source_name = @table,
                            @capture_instance = @inst, @role_name = @role,
                            @supports_net_changes = 0;
                    PRINT CONCAT('Restored CDC (minimal) on [', @schema, '].[', @table, '] / ', @inst);
                END TRY
                BEGIN CATCH
                    SET @failures += CONCAT('[', @schema, '].[', @table, '] / ', @inst, ': ', ERROR_MESSAGE(), CHAR(13), CHAR(10));
                END CATCH
            END CATCH
        END CATCH
    END
    FETCH NEXT FROM cur INTO @schema, @table, @inst, @role, @net, @idx, @fg, @cols;
END
CLOSE cur;
DEALLOCATE cur;

IF @failures <> N''
BEGIN
    -- Keep dbo._cdc_restore so a re-run can retry from the original config.
    DECLARE @msg nvarchar(2048) = LEFT(N'CDC re-enable failed for: ' + CHAR(13) + CHAR(10) + @failures, 2048);
    THROW 50020, @msg, 1;
END

-- Preserve the CDC cleanup retention previously set by the old 000003_CleanupCDC.sql post-deploy.
IF EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_cdc_enabled = 1)
    EXEC sys.sp_cdc_change_job @job_type = N'cleanup', @retention = 72;

DROP TABLE dbo._cdc_restore;
