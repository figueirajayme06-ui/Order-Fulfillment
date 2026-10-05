/*
Post-Deployment Script Template
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.
 Use SQLCMD syntax to include a file in the post-deployment script.
 Example:      :r .\myfile.sql
 Use SQLCMD syntax to reference a variable in the post-deployment script.
 Example:      :setvar TableName MyTable
               SELECT * FROM [$(TableName)]
--------------------------------------------------------------------------------------
*/
-- CDC (enable / initial tables / cleanup retention) is managed by the deploy pipeline
-- (.azd/actions/deploy-sql.yml), which snapshots, disables and faithfully restores CDC around the
-- publish. The DACPAC no longer contains any CDC scripts.
-- The pipeline CDC T-SQL lives in .azd/sql/disable-cdc.sql and .azd/sql/restore-cdc.sql.
-- Do NOT re-add CDC enable/disable scripts to this DACPAC (do not :r them) - see those files.
:r .\Scripts\PostDeployment\000004_Remove_RingFence_Default_Constraint.sql
:r .\Scripts\PostDeployment\000007_ConfigureADGroups.sql
