-- Run explicitly against the database configured by KeyValueDBConnection.
-- Requires SQL Server 2016+ and compatibility level 130+ (ISJSON).
-- This script does not create a database or read application connection strings.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'RestlessFalcon.KeyValueDB.Migrations',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
    IF @lockResult < 0 THROW 50001, 'Could not acquire the migration lock.', 1;

    IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
        CREATE TABLE dbo.SchemaMigrations
        (
            [Version] int NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
            [Name] nvarchar(200) NOT NULL,
            AppliedAt datetimeoffset(7) NOT NULL
                CONSTRAINT DF_SchemaMigrations_AppliedAt DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
        );

    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE [Version] = 1)
    BEGIN
        IF OBJECT_ID(N'dbo.StateEntries', N'U') IS NOT NULL
            THROW 50002, 'StateEntries exists without migration version 1. Inspect the schema before proceeding.', 1;
        CREATE TABLE dbo.StateEntries
        (
            [Namespace] nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
            [Key] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
            -- Envelope {"value": <JSON>} supports objects, arrays, scalars and null on SQL Server 2016+.
            ValueJson nvarchar(max) NOT NULL,
            CreatedAt datetimeoffset(7) NOT NULL
                CONSTRAINT DF_StateEntries_CreatedAt DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
            UpdatedAt datetimeoffset(7) NOT NULL
                CONSTRAINT DF_StateEntries_UpdatedAt DEFAULT TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
            CONSTRAINT PK_StateEntries PRIMARY KEY CLUSTERED ([Namespace], [Key]),
            CONSTRAINT CK_StateEntries_ValidJson CHECK (ISJSON(ValueJson) = 1),
            CONSTRAINT CK_StateEntries_NonEmptyIdentifiers CHECK (LEN([Namespace]) > 0 AND LEN([Key]) > 0)
        );
        INSERT INTO dbo.SchemaMigrations ([Version], [Name]) VALUES (1, N'CreateStateEntries');
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
