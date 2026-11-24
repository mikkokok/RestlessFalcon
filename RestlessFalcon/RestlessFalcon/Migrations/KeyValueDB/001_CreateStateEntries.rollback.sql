-- DESTRUCTIVE: this removes ALL saved state. Back up and stop state writes first.
-- Run explicitly against the KeyValueDB database only; never run this at application startup.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'RestlessFalcon.KeyValueDB.Migrations',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
    IF @lockResult < 0 THROW 50001, 'Could not acquire the migration lock.', 1;

    IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE [Version] > 1)
            THROW 50003, 'Roll back newer migrations before version 1.', 1;
        IF EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE [Version] = 1)
        BEGIN
            IF OBJECT_ID(N'dbo.StateEntries', N'U') IS NOT NULL
                DROP TABLE dbo.StateEntries;
            DELETE FROM dbo.SchemaMigrations WHERE [Version] = 1;
        END;
    END;
    -- Retain the empty migration ledger for future migrations/re-application.
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
