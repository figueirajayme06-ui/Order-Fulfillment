/*
 Pre-Deployment Script Template
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be executed before the build script.
 Use SQLCMD syntax to include a file in the pre-deployment script.
 Example:      :r .\myfile.sql
 Use SQLCMD syntax to reference a variable in the pre-deployment script.
 Example:      :setvar TableName MyTable
               SELECT * FROM [$(TableName)]
--------------------------------------------------------------------------------------
*/
-- CDC is managed by the deploy pipeline (.azd/actions/deploy-sql.yml), not by the DACPAC.
-- It must be disabled before SqlPackage plan analysis (which fails with SQL72035 otherwise),
-- which is earlier than any pre-deploy script can run - so there is no CDC handling here.
-- The pipeline CDC T-SQL lives in .azd/sql/disable-cdc.sql and .azd/sql/restore-cdc.sql.
-- Do NOT re-add CDC enable/disable scripts to this DACPAC (do not :r them) - see those files.