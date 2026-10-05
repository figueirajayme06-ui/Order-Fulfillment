CREATE TABLE [dbo].[Users] (
    [LoginName]    NVARCHAR (100) NOT NULL,
    [FullName]     NVARCHAR (100) NOT NULL,
    [Division]     NVARCHAR (100) NOT NULL,
    [IsAdmin]      BIT            NOT NULL,
    [IsSuperAdmin] BIT            NOT NULL,
    [DateFormat]   NVARCHAR (MAX) NOT NULL,
    [Language]     INT NOT NULL DEFAULT 0, 
    [Roles] NVARCHAR(MAX) NULL, 
    [LastLoginAtUtc] DATETIME2 (7) NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([LoginName] ASC)
);

