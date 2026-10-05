-- Migration: Remove stale CPQ DataRefresh row
-- The CPQ data pipeline has been replaced by the PCatSync Azure Function service
-- which writes directly to the CPQ_* tables. The Last Refreshed panel now sources
-- the CPQ timestamp from CPQ_Version (IsLatest = 1, Application = 'PCatSync').
DELETE FROM [dbo].[DataRefresh] WHERE [Key] = 'CPQ';
