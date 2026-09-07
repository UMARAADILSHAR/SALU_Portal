-- SaluExamPortal - Database Configuration & Security Scripts
-- Execute these scripts after running 'dotnet ef database update'
-- Date: August 30, 2026

-- ===== VERIFICATION SCRIPTS =====

-- 1. Verify TOTP columns were added
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AspNetUsers' 
AND COLUMN_NAME IN ('TotpSecretEncrypted', 'TotpEnabled', 'TotpEnabledAt')
ORDER BY ORDINAL_POSITION;

-- Expected Output:
-- TotpSecretEncrypted | nvarchar(500) | YES
-- TotpEnabled | bit | NO
-- TotpEnabledAt | datetime2 | YES

-- 2. Verify audit log trigger exists
SELECT OBJECT_NAME(object_id) AS TriggerName, 
       OBJECTPROPERTY(object_id, 'ExecIsInsertTrigger') AS IsInsertTrigger,
       OBJECTPROPERTY(object_id, 'ExecIsUpdateTrigger') AS IsUpdateTrigger,
       OBJECTPROPERTY(object_id, 'ExecIsDeleteTrigger') AS IsDeleteTrigger
FROM sys.sql_modules
WHERE definition LIKE '%AuditLogPreventModification%';

-- Expected Output:
-- TriggerName: tr_AuditLogPreventModification
-- IsInsertTrigger: 0 (not insert)
-- IsUpdateTrigger: 1 (prevents update)
-- IsDeleteTrigger: 1 (prevents delete)

-- 3. Verify indexes created for audit logs
SELECT INDEX_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_NAME = 'AuditLogs'
AND INDEX_NAME IN ('IX_AuditLogs_UserId_CreatedAt', 'IX_AuditLogs_Action',
                    'IX_MakerCheckerRequests_ReviewStatus_CreatedAt')
ORDER BY INDEX_NAME, SEQ_IN_INDEX;

-- ===== SECURITY CONFIGURATION =====

-- 4. Set database owner to admin user
ALTER AUTHORIZATION ON DATABASE::[aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235] 
TO [sa];

-- 5. Enable password policy for SQL Server logins (if using SQL authentication)
-- Create application service account (recommended over sa)
CREATE LOGIN [SaluPortalAppUser] WITH PASSWORD = 'StrongAppPassword@2024';
CREATE USER [SaluPortalAppUser] FOR LOGIN [SaluPortalAppUser];
ALTER ROLE [db_owner] ADD MEMBER [SaluPortalAppUser];

-- 6. Audit Configuration - Create audit specification
-- Note: Requires SQL Server Enterprise or Standard Edition with Audit enabled
CREATE SERVER AUDIT [SaluExamPortalAudit]
    TO FILE (FILEPATH = 'C:\Audit\' , MAXSIZE = 1024 MB)
    WITH (QUEUE_DELAY = 1000, ON_FAILURE = CONTINUE);

ALTER SERVER AUDIT [SaluExamPortalAudit] WITH (STATE = ON);

-- 7. Create database audit specification
CREATE DATABASE AUDIT SPECIFICATION [SaluExamPortal_Audit]
FOR SERVER AUDIT [SaluExamPortalAudit]
ADD (
    SELECT ON OBJECT::[dbo].[AspNetUsers] BY [public],
    SELECT ON OBJECT::[dbo].[AuditLogs] BY [public],
    SELECT ON OBJECT::[dbo].[MakerCheckerRequests] BY [public],
    SELECT ON OBJECT::[dbo].[Enrollments] BY [public]
)
WITH (STATE = ON);

-- ===== DATA CLEANUP & VALIDATION =====

-- 8. Clean up test users (if any exist from development)
DELETE FROM [AspNetUserLogins] WHERE UserId NOT IN (
    SELECT Id FROM [AspNetUsers] WHERE IsVerified = 1
);

DELETE FROM [AspNetUserClaims] WHERE UserId NOT IN (
    SELECT Id FROM [AspNetUsers] WHERE IsVerified = 1
);

DELETE FROM [AspNetUsers] WHERE IsVerified = 0 AND CreatedAt < DATEADD(DAY, -30, GETUTCDATE());

-- 9. Ensure admin user exists
DECLARE @AdminEmail NVARCHAR(256) = 'admin@saluexamportal.edu.pk';

IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE Email = @AdminEmail)
BEGIN
    PRINT 'WARNING: Admin user does not exist! Run DbInitializer to create it.';
END
ELSE
BEGIN
    PRINT 'Admin user exists: ' + @AdminEmail;
    SELECT Id, Email, TotpEnabled, IsVerified FROM [AspNetUsers] WHERE Email = @AdminEmail;
END

-- 10. Verify enrollment data integrity
SELECT 
    COUNT(*) AS TotalEnrollments,
    SUM(CASE WHEN UserId IS NULL THEN 1 ELSE 0 END) AS MissingUser,
    SUM(CASE WHEN AcademicYearId IS NULL THEN 1 ELSE 0 END) AS MissingAcademicYear,
    SUM(CASE WHEN Status IS NULL THEN 1 ELSE 0 END) AS MissingStatus
FROM [Enrollments];

-- ===== BACKUP & RECOVERY =====

-- 11. Create full backup (run after initial setup)
BACKUP DATABASE [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235]
TO DISK = N'C:\Backups\SaluExamPortal_Full_20260830.bak'
WITH NOFORMAT, NOINIT, NAME = N'SaluExamPortal-Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10;

-- 12. Create transaction log backup (run regularly for point-in-time recovery)
BACKUP LOG [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235]
TO DISK = N'C:\Backups\SaluExamPortal_Log_20260830.bak'
WITH NOFORMAT, NOINIT, NAME = N'SaluExamPortal-Log Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 5;

-- 13. Verify backup integrity
RESTORE VERIFYONLY FROM DISK = N'C:\Backups\SaluExamPortal_Full_20260830.bak';

-- ===== PERFORMANCE BASELINE =====

-- 14. Create missing index recommendations baseline
SELECT 
    OBJECT_NAME(s.object_id) AS TableName,
    s.name AS IndexName,
    s.type_desc AS IndexType,
    CAST(s.user_updates AS DECIMAL(10,0)) AS UserUpdates,
    CAST(s.user_seeks AS DECIMAL(10,0)) AS UserSeeks,
    CAST(s.user_scans AS DECIMAL(10,0)) AS UserScans
FROM sys.dm_db_index_usage_stats s
WHERE database_id = DB_ID()
ORDER BY s.user_seeks + s.user_scans + s.user_lookups DESC;

-- 15. Check database size
EXEC sp_spaceused;

-- 16. Index fragmentation status
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent AS FragmentationPercent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id 
    AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10
    AND ips.page_count > 1000
ORDER BY ips.avg_fragmentation_in_percent DESC;

-- ===== ADMIN OPERATIONS =====

-- 17. Reset admin password (manual recovery)
-- NOTE: Must set SeedAdmin:Password before running DbInitializer
-- Then delete admin user and restart app
DELETE FROM [AspNetUsers] 
WHERE Email = 'admin@saluexamportal.edu.pk';
-- Application restart will recreate admin with configured password

-- 18. Unlock locked user account
DECLARE @UserId NVARCHAR(450) = 'user-id-here';
UPDATE [AspNetUsers]
SET LockoutEnd = NULL,
    AccessFailedCount = 0
WHERE Id = @UserId;

-- 19. Audit log viewer - Recent activity
SELECT TOP 100
    CreatedAt,
    UserId,
    Action,
    Entity,
    EntityId,
    Details
FROM [AuditLogs]
ORDER BY CreatedAt DESC;

-- 20. Maker-Checker request status
SELECT 
    Id,
    RequestType,
    TargetEntityName,
    ReviewStatus,
    MakerUserId,
    CheckerUserId,
    MakerSubmittedAt,
    CheckerReviewedAt
FROM [MakerCheckerRequests]
WHERE ReviewStatus = 'Pending'
ORDER BY MakerSubmittedAt DESC;

-- ===== MAINTENANCE SCRIPTS =====

-- 21. Weekly maintenance job script
-- Schedule this to run weekly (e.g., Sunday 2 AM)
BEGIN TRANSACTION;

-- Rebuild fragmented indexes
EXEC sp_MSForEachTable 'ALTER INDEX ALL ON ? REBUILD';

-- Update statistics
EXEC sp_MSForEachDB 'USE [?]; EXEC sp_updatestats;';

-- Cleanup old audit logs (keep 1 year)
DELETE FROM [AuditLogs]
WHERE CreatedAt < DATEADD(YEAR, -1, GETUTCDATE());

-- Cleanup expired requests
DELETE FROM [MakerCheckerRequests]
WHERE ReviewStatus = 'Pending'
    AND MakerSubmittedAt < DATEADD(DAY, -1, GETUTCDATE());

-- Backup log
BACKUP LOG [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235]
TO DISK = N'C:\Backups\SaluExamPortal_Log_Weekly.bak'
WITH INIT, NAME = N'SaluExamPortal-Weekly Log Backup', SKIP, NOREWIND, NOUNLOAD;

COMMIT TRANSACTION;

-- ===== DISASTER RECOVERY =====

-- 22. Restore from full backup (in case of data corruption)
-- Step 1: Put database in SINGLE_USER mode
ALTER DATABASE [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235] 
SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

-- Step 2: Restore full backup
RESTORE DATABASE [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235]
FROM DISK = N'C:\Backups\SaluExamPortal_Full_20260829.bak'
WITH NORECOVERY, REPLACE;

-- Step 3: Restore latest transaction log (for point-in-time recovery)
-- RESTORE LOG [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235]
-- FROM DISK = N'C:\Backups\SaluExamPortal_Log_20260830.bak'
-- WITH RECOVERY;

-- Step 4: Return to MULTI_USER mode
ALTER DATABASE [aspnet-SaluExamPortal-3a3e61c2-9cc4-4e17-9591-2b361ede2235] 
SET MULTI_USER;

-- ===== VERIFICATION FINAL =====

-- 23. Final system check
SELECT
    DB_NAME() AS DatabaseName,
    GETUTCDATE() AS CheckDate,
    (SELECT COUNT(*) FROM [AspNetUsers]) AS UserCount,
    (SELECT COUNT(*) FROM [Enrollments]) AS EnrollmentCount,
    (SELECT COUNT(*) FROM [AuditLogs]) AS AuditLogCount,
    (SELECT COUNT(*) FROM [MakerCheckerRequests]) AS PendingRequests,
    (SELECT CAST(SUM(size * 8 / 1024.0) AS NUMERIC(10, 2)) FROM sys.database_files) AS DatabaseSizeMB;

PRINT '✓ Database verification complete!';
PRINT 'All security configurations have been applied successfully.';
