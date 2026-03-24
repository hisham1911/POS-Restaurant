# Quick Start Deployment Guide

## TL;DR - Deploy in 5 Minutes

This guide gets the fixes deployed quickly. For details, see `COMPLETE_FIX_SUMMARY.md`.

---

## Step 1: Backup (30 seconds)

```powershell
# Backup database
cp backend/KasserPro.API/kasserpro.db backend/KasserPro.API/kasserpro.db.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')
```

---

## Step 2: Stop Backend (10 seconds)

```powershell
# Find and stop dotnet process
Get-Process dotnet | Where-Object {$_.Path -like "*KasserPro*"} | Stop-Process -Force
```

---

## Step 3: Start Backend (30 seconds)

```powershell
cd backend/KasserPro.API
dotnet run
```

Wait for:
```
[INF] Now listening on: http://localhost:5243
```

---

## Step 4: Verify Frontend (10 seconds)

1. Open browser: `http://localhost:3000`
2. Check console (F12) - should see no errors
3. Frontend auto-reloads with new changes

---

## Step 5: Quick Test (2 minutes)

### Test 1: Error Display Works
1. Go to POS
2. Try to complete order with invalid data
3. ✅ Should see error toast in Arabic

### Test 2: Cash Sale Works
1. Create new order
2. Add products
3. Complete with cash payment
4. ✅ Should succeed and print receipt

### Test 3: Credit Sale Works
1. Create order for customer with credit
2. Complete with Credit payment (amount = 0)
3. ✅ Should succeed if within credit limit
4. ✅ Should show error toast if exceeds limit

---

## Step 6: Monitor (5 minutes)

Watch backend logs for:
```
[INF] Cash register transaction recorded: Sale - {amount}
```

Should NOT see:
```
[ERR] The connection is already in a transaction
```

---

## Success Checklist

- [ ] Backend started successfully
- [ ] Frontend loads without errors
- [ ] Error toasts display properly
- [ ] Cash sales complete successfully
- [ ] Credit sales work or show proper errors
- [ ] No transaction errors in logs

---

## If Something Goes Wrong

### Rollback (1 minute)

```powershell
# Stop backend
Get-Process dotnet | Where-Object {$_.Path -like "*KasserPro*"} | Stop-Process -Force

# Restore database
cp backend/KasserPro.API/kasserpro.db.backup-* backend/KasserPro.API/kasserpro.db

# Revert code
cd backend
git checkout HEAD~1 -- KasserPro.Infrastructure/Repositories/UnitOfWork.cs
git checkout HEAD~1 -- KasserPro.Application/Services/Implementations/OrderService.cs
git checkout HEAD~1 -- KasserPro.Application/Common/Interfaces/IUnitOfWork.cs

cd ../frontend
git checkout HEAD~1 -- src/utils/errorHandler.ts
git checkout HEAD~1 -- src/api/baseApi.ts

# Restart
cd ../backend/KasserPro.API
dotnet run
```

---

## Common Issues

### Issue: Backend won't start
**Solution**: Check if port 5243 is already in use
```powershell
Get-NetTCPConnection -LocalPort 5243
```

### Issue: Frontend shows old code
**Solution**: Hard refresh browser (Ctrl+Shift+R)

### Issue: Database locked
**Solution**: Make sure no other process is using the database
```powershell
Get-Process | Where-Object {$_.Path -like "*sqlite*"}
```

---

## What Changed?

### Frontend
- Fixed toast library (now uses `sonner`)
- Added error handling for mutations

### Backend
- Enhanced transaction management
- Prevents nested transactions
- Automatic cleanup

---

## Need Help?

1. Check `COMPLETE_FIX_SUMMARY.md` for overview
2. Check `backend/TRANSACTION_MANAGEMENT_FIX.md` for technical details
3. Check `backend/TRANSACTION_BEST_PRACTICES.md` for development guide

---

## Done!

If all tests passed, you're good to go. The system is now:
- ✅ Showing error messages properly
- ✅ Handling transactions correctly
- ✅ Self-healing on failures

**Deployment Time**: ~5 minutes
**Status**: Production Ready ✅
