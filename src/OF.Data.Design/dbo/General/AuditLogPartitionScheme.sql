CREATE PARTITION SCHEME AuditLogPS
AS PARTITION AuditLogTableNamePF
TO ([Primary], [Primary], [Primary], [Primary]);