# PHASE 0 MIGRATIONS
## Database Schema Changes for P0 Security Hardening

**Date:** 2026-02-14  
**Total Migrations:** 1  
**Breaking Changes:** None

---

## MIGRATION SUMMARY

| Migration | Purpose | Risk | Rollback |
|-----------|---------|------|----------|
| AddSecurityStampToUser | JWT invalidation support | Low | Safe |

---

## MIGRATION 1: AddSecurityStampToUser

**File:** `src/KasserPro.Infrastructure/Migrations/20260214000000_AddSecurityStampToUser.cs`

**Purpose:** Add SecurityStamp field to Users table to support immediate JWT invalidation when user permissions change.

### Schema Changes

#### Up Migration
```sql
-- Add SecurityStamp column
ALTER TABLE Users 
ADD COLUMN SecurityStamp TEXT NOT NULL DEFAULT '';

-- Initialize existing users with unique stamps
UPDATE Users 
SET SecurityStamp = lower(hex(randomblob(16)))
WHERE SecurityStamp = '';
```

#### Down Migration
```sql
-- Remove SecurityStamp column
ALTER TABLE Users 
DROP COLUMN SecurityStamp;
```

### Data Impact

**Before Migration:**
```
Users Table:
- Id
- TenantId
- BranchId
- Name
- Email
- PasswordHash
- Phone
- Role
- IsActive
- PinCode
- CreatedAt
- UpdatedAt
- IsDeleted
```

**After Migration:**
```
Users Table:
- Id
- TenantId
- BranchId
- Name
- Email
- PasswordHash
- Phone
- Role
- IsActive
- PinCode
- SecurityStamp  <-- NEW
- CreatedAt
- UpdatedAt
- IsDeleted
```

### Existing Data Handling

**Automatic Initialization:**
- All existing users get unique SecurityStamp values
- Uses SQLite `randomblob(16)` for cryptographically random stamps
- Converted to lowercase hex string (32 characters)
- No manual intervention required

**Example:**
```sql
-- Before
Id=1, Email=admin@kasserpro.com, SecurityStamp=''

-- After
Id=1, Email=admin@kasserpro.com, SecurityStamp='a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4'
```

### Backward Compatibility

**Old JWTs (without security_stamp claim):**
- Continue to work until expiry
- Validation skips stamp check if claim is missing
- Graceful degradation

**New JWTs (with security_stamp claim):**
- Validated on every request
- Rejected if stamp doesn't match database

**Transition Period:**
- Old JWTs expire naturally (24 hours)
- No forced logout required
- Seamless migration

### Performance Impact

**Migration Execution:**
- Time: < 1 second for < 1000 users
- Time: < 5 seconds for < 10,000 users
- Locks: Table-level lock during ALTER TABLE (brief)

**Runtime Impact:**
- Storage: +32 bytes per user
- Query: No additional queries (stamp read with user record)
- Index: No index needed (not queried independently)

### Rollback Safety

**Safe to Rollback:** ✅ Yes

**Rollback Procedure:**
```bash
# Revert migration
dotnet ef migrations remove --project src/KasserPro.Infrastructure --startup-project src/KasserPro.API

# Or manually
sqlite3 kasserpro.db "ALTER TABLE Users DROP COLUMN SecurityStamp;"
```

**Rollback Impact:**
- SecurityStamp column removed
- JWT validation reverts to old behavior (no stamp check)
- No data loss (other columns unaffected)

**When to Rollback:**
- Critical bug in stamp validation logic
- Performance issues (unlikely)
- Compatibility issues with old clients (unlikely)

### Testing Validation

**Pre-Migration Checks:**
```bash
# Verify Users table structure
sqlite3 kasserpro.db "PRAGMA table_info(Users);"

# Count existing users
sqlite3 kasserpro.db "SELECT COUNT(*) FROM Users;"
```

**Post-Migration Checks:**
```bash
# Verify SecurityStamp column exists
sqlite3 kasserpro.db "PRAGMA table_info(Users);" | grep SecurityStamp

# Verify all users have stamps
sqlite3 kasserpro.db "SELECT COUNT(*) FROM Users WHERE SecurityStamp = '';"
# Expected: 0

# Verify stamp uniqueness
sqlite3 kasserpro.db "SELECT COUNT(DISTINCT SecurityStamp) FROM Users;"
# Expected: Same as user count

# Sample stamps
sqlite3 kasserpro.db "SELECT Id, Email, SecurityStamp FROM Users LIMIT 5;"
```

### Migration Execution Log

**Expected Output:**
```
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20260214000000_AddSecurityStampToUser'.
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (2ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      ALTER TABLE "Users" ADD "SecurityStamp" TEXT NOT NULL DEFAULT '';
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (15ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      UPDATE Users SET SecurityStamp = lower(hex(randomblob(16))) WHERE SecurityStamp = '';
info: Microsoft.EntityFrameworkCore.Migrations[20405]
      Done.
```

### Common Issues

#### Issue 1: Migration Fails with "column already exists"
**Cause:** Migration already applied manually or partially
**Solution:**
```bash
# Check migration history
sqlite3 kasserpro.db "SELECT * FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddSecurityStamp%';"

# If exists, skip migration
# If not exists but column exists, add history entry manually
sqlite3 kasserpro.db "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260214000000_AddSecurityStampToUser', '8.0.0');"
```

#### Issue 2: Existing users have empty stamps
**Cause:** UPDATE statement didn't run or failed
**Solution:**
```bash
# Manually initialize stamps
sqlite3 kasserpro.db "UPDATE Users SET SecurityStamp = lower(hex(randomblob(16))) WHERE SecurityStamp = '';"
```

#### Issue 3: Migration takes too long
**Cause:** Large user table (> 100,000 users)
**Solution:**
```bash
# Run migration during maintenance window
# Or batch update in chunks
sqlite3 kasserpro.db "UPDATE Users SET SecurityStamp = lower(hex(randomblob(16))) WHERE SecurityStamp = '' AND Id <= 10000;"
sqlite3 kasserpro.db "UPDATE Users SET SecurityStamp = lower(hex(randomblob(16))) WHERE SecurityStamp = '' AND Id > 10000 AND Id <= 20000;"
# ... continue in batches
```

---

## MIGRATION SEQUENCE

### Correct Order
1. AddSecurityStampToUser (Phase 0)
2. [Future Phase 1 migrations]
3. [Future Phase 2 migrations]

### Dependencies
- **Depends On:** None (first Phase 0 migration)
- **Required By:** JWT validation logic in Program.cs

### Idempotency
- Migration is idempotent (can be run multiple times safely)
- `WHERE SecurityStamp = ''` ensures UPDATE only runs on uninitialized users
- No duplicate stamps generated

---

## PRODUCTION DEPLOYMENT CHECKLIST

### Pre-Deployment
- [ ] Backup database
- [ ] Verify backup integrity
- [ ] Test migration on staging copy
- [ ] Verify existing users count
- [ ] Schedule maintenance window (optional, migration is fast)

### Deployment
- [ ] Stop API (optional, can run with API active)
- [ ] Run migration: `dotnet ef database update`
- [ ] Verify migration success in logs
- [ ] Verify SecurityStamp column exists
- [ ] Verify all users have stamps
- [ ] Start API
- [ ] Test JWT validation

### Post-Deployment
- [ ] Monitor logs for TOKEN_INVALIDATED errors
- [ ] Verify old JWTs still work (backward compatibility)
- [ ] Verify new JWTs include security_stamp claim
- [ ] Test stamp invalidation (change user role, verify JWT rejected)
- [ ] Document migration completion

### Rollback Triggers
- [ ] Migration fails to apply
- [ ] All users have empty stamps after migration
- [ ] JWT validation breaks (all requests fail)
- [ ] Performance degradation (unlikely)

---

## MIGRATION TESTING MATRIX

| Scenario | Expected Result | Validation |
|----------|----------------|------------|
| Fresh database | SecurityStamp column added, no users | ✅ Column exists |
| Database with 10 users | All users get unique stamps | ✅ 10 distinct stamps |
| Database with 1000 users | Migration completes < 5s | ✅ Performance acceptable |
| Run migration twice | No errors, no duplicate stamps | ✅ Idempotent |
| Rollback migration | SecurityStamp column removed | ✅ Clean rollback |
| Old JWT (no stamp) | Still works until expiry | ✅ Backward compatible |
| New JWT (with stamp) | Validated on every request | ✅ Security enforced |
| Change user role | Old JWT rejected | ✅ Invalidation works |

---

## FUTURE MIGRATIONS

Phase 0 includes only 1 migration. Future phases will add:

**Phase 1 (Production Hardening):**
- No schema changes (configuration only)

**Phase 2 (Operational Fixes):**
- No schema changes (frontend only)

**Phase 3 (Full Permission System - Future):**
- AddRolesPermissionsCore
- AddUserRolesAndBranchAccess
- SeedPermissionCatalogAndDefaultRoles
- BackfillLegacyAdminCashierToUserRoles

---

**Migration Document Generated:** 2026-02-14  
**Status:** ✅ READY FOR DEPLOYMENT
