# توثيق شامل لميزة النسخ الاحتياطي والاستعادة

هذا المستند يوضح كل ما يتعلق بميزة النسخ الاحتياطي والاستعادة في النظام، سواء في الخلفية (Backend) أو الواجهة (Frontend). الهدف هو توحيد المعرفة الفنية، تسهيل الصيانة، وضمان تطبيق الخطوات الصحيحة عند التطوير أو الإصلاح.

---

## 1. نظرة عامة (Overview)

الميزة توفر:

- إنشاء نسخة احتياطية من قاعدة البيانات أثناء التشغيل (Hot Backup).
- فحص سلامة النسخة الاحتياطية (`integrity_check`).
- استعادة قاعدة البيانات مع تفعيل وضع الصيانة.
- إنشاء نسخة أمان قبل الاستعادة (`pre-restore`).
- تطبيق كل الـ Migrations بعد الاستعادة.
- التحقق من صحة البيانات بعد الاستعادة.
- عرض نتائج الاستعادة في الواجهة.
- النسخ الاحتياطي التلقائي اليومي (Scheduled Backup).

---

## 2. Backend - الخدمات الأساسية

### 2.1 BackupService

**المسار:**

- `backend/KasserPro.Infrastructure/Services/BackupService.cs`

**المهام الأساسية:**

- إنشاء نسخة احتياطية باستخدام SQLite Backup API.
- فحص سلامة النسخة الاحتياطية.
- إدارة الاحتفاظ بالنسخ القديمة (Retention).
- توفير قائمة بالنسخ الاحتياطية.

**المنطق الأساسي:**

- يستخدم:
  - `SqliteConnection.BackupDatabase(...)`
  - `PRAGMA integrity_check;`
- يحفظ النسخ في مجلد: `backups/` داخل ContentRootPath.
- يقوم بتسمية الملفات بناءً على السبب:
  - `pre-migration` (قبل تطبيق الميجريشن عند الإقلاع)
  - `pre-restore` (قبل الاستعادة)
  - `daily-scheduled` (النسخ اليومية التلقائية)
  - `manual` (عند إنشاء نسخة يدوية)

**اسم الملف النموذجي:**

```
KasserPro-backup-20260225-142210-pre-migration.db
KasserPro-backup-20260225-142210-pre-restore.db
KasserPro-backup-20260225-020000-daily-scheduled.db
KasserPro-backup-20260225-142210.db
```

---

### 2.2 RestoreService

**المسار:**

- `backend/KasserPro.Infrastructure/Services/RestoreService.cs`

**المهام الأساسية:**

- فحص سلامة نسخة الاستعادة قبل استخدامها.
- تفعيل وضع الصيانة قبل الاستبدال.
- إنشاء نسخة أمان (`pre-restore`).
- استبدال قاعدة البيانات.
- تطبيق جميع التحديثات (Migrations).
- فحص صحة البيانات بعد الاستعادة.
- تعطيل وضع الصيانة.

**تسلسل الاستعادة:**

1. التحقق من وجود ملف النسخة.
2. فحص سلامة الملف.
3. تفعيل وضع الصيانة (maintenance.lock).
4. إنشاء نسخة أمان قبل الاستعادة.
5. مسح WAL/SHM وإغلاق الاتصالات.
6. نسخ ملف النسخة فوق قاعدة البيانات.
7. تنفيذ جميع الميجريشن.
8. فحص البيانات (Validation).
9. تعطيل وضع الصيانة.

---

### 2.3 DataValidationService

**المسار:**

- `backend/KasserPro.Infrastructure/Services/DataValidationService.cs`

**الهدف:**

- التأكد من أن القيم الحرجة بعد الاستعادة متوافقة مع نوع البيانات المتوقع.

**التحقق الحالي يشمل:**

- `Products.Price` (سعر المنتج) يجب أن يكون رقمي.
- `Products.StockQuantity` (كمية المخزون) يجب أن تكون رقمية.
- `Orders.Total` (إجمالي الفاتورة) يجب أن يكون رقمياً.

**آلية الفحص:**

- استخدام `typeof()` في SQLite لمعرفة نوع التخزين.
- استخدام نمط `GLOB` لاكتشاف النصوص غير الرقمية.
- أي مشكلة يتم تسجيلها في اللوج وإرجاع عدد المشاكل للواجهة.

---

### 2.4 MaintenanceModeMiddleware

**المسار:**

- `backend/KasserPro.API/Middleware/MaintenanceModeMiddleware.cs`

**الهدف:**

- منع جميع الطلبات أثناء الاستعادة.
- السماح فقط بـ `/health` أثناء وضع الصيانة.

**الآلية:**

- إذا وُجد ملف `maintenance.lock` في `ContentRootPath`:
  - يتم رد 503 على جميع الطلبات.

---

### 2.5 DailyBackupBackgroundService

**المسار:**

- `backend/KasserPro.Infrastructure/Services/DailyBackupBackgroundService.cs`

**الهدف:**

- إنشاء نسخة احتياطية يومية تلقائية الساعة 2:00 صباحاً.
- تشغيل تنظيف النسخ القديمة بعد الإنشاء.

---

## 3. Backend - Controllers

### AdminController

**المسار:**

- `backend/KasserPro.API/Controllers/AdminController.cs`

**الـ Endpoints:**

- `POST /api/admin/backup` → إنشاء نسخة احتياطية يدوية.
- `GET /api/admin/backups` → عرض قائمة النسخ الاحتياطية.
- `POST /api/admin/restore` → استعادة نسخة احتياطية.

**الصلاحيات:**

- `backup` و `listBackups`: Admin أو SystemOwner.
- `restore`: SystemOwner فقط.

---

## 4. Backend - النماذج (DTOs)

**BackupResult**

- `Success`
- `BackupPath`
- `BackupSizeBytes`
- `BackupTimestamp`
- `Reason`
- `IntegrityCheckPassed`
- `ErrorMessage`

**RestoreResult**

- `Success`
- `RestoredFromPath`
- `PreRestoreBackupPath`
- `RestoreTimestamp`
- `MaintenanceModeEnabled`
- `RequiresRestart`
- `MigrationsApplied`
- `DataValidationIssuesFound`
- `ErrorMessage`

---

## 5. Frontend - API Client

**المسار:**

- `frontend/src/api/backupApi.ts`

**الـ Endpoints:**

- `createBackup` → POST `/api/admin/backup`
- `listBackups` → GET `/api/admin/backups`
- `restoreBackup` → POST `/api/admin/restore`

**الـ Types المستخدمة:**

- `BackupInfo`
- `BackupResult`
- `RestoreResult`

---

## 6. Frontend - صفحة النسخ الاحتياطي

**المسار:**

- `frontend/src/pages/backup/BackupPage.tsx`

**الخصائص:**

- عرض قائمة النسخ الاحتياطية.
- زر إنشاء نسخة احتياطية يدوياً.
- زر استعادة مع نافذة تأكيد.
- نافذة نجاح تُظهر:
  - عدد الـ migrations التي تم تطبيقها.
  - عدد مشاكل التحقق من البيانات.

**الحالات المعروضة:**

- ✅ إذا لم توجد مشاكل (DataValidationIssuesFound = 0).
- ⚠️ إذا تم اكتشاف مشاكل (DataValidationIssuesFound > 0).

---

## 7. الربط بين الـ Backend والـ Frontend

- الـ Backend يرجع نتائج الاستعادة ضمن `RestoreResult`.
- الـ Frontend يقرأ القيم ويعرضها للمستخدم.
- أي تحذير يظهر للمستخدم في الـ Modal بعد نجاح الاستعادة.

---

## 8. مسار النسخ الاحتياطية

- يتم تخزين النسخ في:
  - `ContentRootPath/backups/`
- إذا لم يوجد المجلد يتم إنشاؤه تلقائياً.

---

## 9. نصائح تشغيلية

- **لا تقم باستعادة نسخة من إصدار قديم دون وجود migration.**
- تأكد أن كل إصدار جديد يحتوي على migration مطابق للتغييرات.
- عند ظهور تحذيرات في نتائج الاستعادة، راجع اللوج مباشرة.

---

## 10. مراجعة ما يجب اختباره

- إنشاء نسخة احتياطية يدوية.
- التأكد من سلامة النسخة (`integrity_check`).
- استعادة نسخة قديمة مع وجود migrations.
- رؤية نتيجة الميجريشن في واجهة المستخدم.
- التأكد من ظهور التحذيرات إذا وجدت بيانات غير صالحة.
- اختبار النسخ اليومية التلقائية.

---

## 11. ملاحظات مستقبلية

- يمكن إضافة خيار تنزيل النسخة الاحتياطية من الواجهة.
- يمكن إضافة تشفير للنسخ الاحتياطية لمزيد من الأمان.
- يمكن إضافة تقارير مفصلة عن نتائج الاستعادة.

---

آخر تحديث: 25 فبراير 2026
