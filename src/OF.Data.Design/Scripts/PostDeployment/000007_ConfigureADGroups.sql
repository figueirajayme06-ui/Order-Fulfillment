-- Azure AD Groups Configuration Script
-- Default fallback values (will be overridden by pipeline /v: parameters)
:setvar DbEnvironment "Dev"
:setvar WriterGroupName "GitHub-IPG CD"  
:setvar ReaderGroupName "GitHub-IPG CD"

DECLARE @Environment NVARCHAR(50) = '$(DbEnvironment)'
DECLARE @WriterGroupName NVARCHAR(256) = '$(WriterGroupName)'
DECLARE @ReaderGroupName NVARCHAR(256) = '$(ReaderGroupName)'

PRINT 'Starting Azure AD Groups Role Configuration...'
PRINT 'Target Environment: ' + @Environment
PRINT 'Writer Group: ' + @WriterGroupName
PRINT 'Reader Group: ' + @ReaderGroupName

IF @Environment IN ('Dev', 'SIT')
BEGIN
    PRINT 'Configuring roles for ' + @Environment + ' environment...'
    
    -- Verify users exist (should be pre-created)
    IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = @ReaderGroupName)
    BEGIN
        PRINT 'WARNING: Reader group not found: ' + @ReaderGroupName;
        PRINT 'This Azure AD group should be pre-created by a DBA with CREATE USER [' + @ReaderGroupName + '] FROM EXTERNAL PROVIDER';
    END
    ELSE
    BEGIN
        PRINT 'Reader group exists: ' + @ReaderGroupName;
    END

    IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = @WriterGroupName AND name != @ReaderGroupName)
    BEGIN
        IF @WriterGroupName != @ReaderGroupName
        BEGIN
            PRINT 'WARNING: Writer group not found: ' + @WriterGroupName;
            PRINT 'This Azure AD group should be pre-created by a DBA with CREATE USER [' + @WriterGroupName + '] FROM EXTERNAL PROVIDER';
        END
    END
    ELSE
    BEGIN
        PRINT 'Writer group exists: ' + @WriterGroupName;
    END

    IF NOT IS_ROLEMEMBER('db_datareader', @ReaderGroupName) = 1
    BEGIN
        DECLARE @AddReaderSQL NVARCHAR(MAX) = 'ALTER ROLE db_datareader ADD MEMBER [' + @ReaderGroupName + ']'
        PRINT 'Executing: ' + @AddReaderSQL
        EXEC sp_executesql @AddReaderSQL
        PRINT 'Added db_datareader role to ' + @ReaderGroupName;
    END
    ELSE
    BEGIN
        PRINT 'User ' + @ReaderGroupName + ' already has db_datareader role';
    END

    IF NOT IS_ROLEMEMBER('db_datawriter', @WriterGroupName) = 1
    BEGIN
        DECLARE @AddWriterSQL NVARCHAR(MAX) = 'ALTER ROLE db_datawriter ADD MEMBER [' + @WriterGroupName + ']'
        PRINT 'Executing: ' + @AddWriterSQL
        EXEC sp_executesql @AddWriterSQL
        PRINT 'Added db_datawriter role to ' + @WriterGroupName;
    END
    ELSE
    BEGIN
        PRINT 'User ' + @WriterGroupName + ' already has db_datawriter role';
    END

    PRINT 'Role configuration completed in [' + @Environment + '] environment for Reader Group = [' + @ReaderGroupName + '] and Writer Group = [' + @WriterGroupName + ']';
END
ELSE
BEGIN
    PRINT 'Skipping role configuration in [' + @Environment + '] environment for Reader Group = [' + @ReaderGroupName + '] and Writer Group = [' + @WriterGroupName + ']';
END

-- Verify the users and their roles
SELECT 
    dp.name AS principal_name,
    dp.type_desc AS principal_type,
    r.name AS role_name
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members rm ON dp.principal_id = rm.member_principal_id
LEFT JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
WHERE dp.type IN ('E', 'X')
ORDER BY dp.name, r.name;

GO