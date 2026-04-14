# تقرير جميع التعديلات بعد آخر Commit

## نطاق التقرير

- هذا التقرير يوثق كل التعديلات الحالية في الـ working tree منذ آخر commit.
- يشمل التعديلات سواء كانت من نفس الجلسة أو من أي مصدر آخر.
- مصدر البيانات: `git status --short` و `git diff --numstat` + قراءة الفروقات الفعلية للملفات المعدلة.

## ملخص سريع

- ملفات معدلة (Tracked): 19
- ملفات جديدة غير متتبعة (Untracked): 3
- إجمالي الملفات المتأثرة: 22
- إجمالي الأسطر في الملفات المعدلة (Tracked فقط):
  - إضافات: 1052
  - حذوفات: 474
- أسطر الملفات الجديدة غير المتتبعة (تقريبي بعدد أسطر الملف): 420
- إجمالي التغيير الكلي التقريبي (إضافات + حذوفات + ملفات جديدة): 1946 سطر

## تفاصيل الملفات المعدلة (Tracked)

| الملف                                                                  |   + |   - | ملخص التعديلات                                                                                                                                                                                                                                                                         |
| ---------------------------------------------------------------------- | --: | --: | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Deployment/Scripts/BUILD_ALL.ps1`                                     | 101 |  40 | تحديث السكربت إلى v2.1 مع إضافة `Invoke-CheckedCommand` لفحص أكواد الخروج، والتحقق المسبق من وجود أدوات/مسارات البناء، وتحسين صلابة خطوات النشر وبناء المثبتات.                                                                                                                        |
| `backend/KasserPro.API/Program.cs`                                     | 231 |  31 | إضافة تهيئة SQLCipher وإجبار مزود التشفير، قراءة كلمة مرور DB من connection string أو config أو environment، تحويل DB الحالية إلى مشفرة عند الحاجة، تحسين مسار اللوج، تحسينات pipeline للـ seeding (تنظيف ChangeTracker، مزامنة مخزون tenants المستهدفة فقط، وإغلاق أي ورديات مفتوحة). |
| `backend/KasserPro.API/appsettings.example.json`                       |   3 |   0 | إضافة قسم `DatabaseEncryption.Password` لتوثيق إعداد كلمة مرور تشفير قاعدة البيانات.                                                                                                                                                                                                   |
| `backend/KasserPro.API/packages.lock.json`                             |   7 |  23 | تحديث الرسم البياني للحزم لاستبدال حزم sqlite الافتراضية بحزم SQLCipher المناسبة.                                                                                                                                                                                                      |
| `backend/KasserPro.BridgeApp/packages.lock.json`                       |  60 |   0 | تحديث lockfile بإضافات dependencies مرتبطة ببناء/استهدافات معينة (خصوصا windows7/x86) وإضافة ILLink tasks.                                                                                                                                                                             |
| `backend/KasserPro.Infrastructure/Data/ButcherDataSeeder.cs`           |  17 |  27 | إزالة الاعتماد على كلمة مرور ثابتة لـ System Owner واستبدالها بمصدر ديناميكي (env/توليد آمن)، مع تعديل أيقونة فئة الأحشاء وتوحيد حالة الورديات كمغلقة.                                                                                                                                 |
| `backend/KasserPro.Infrastructure/Data/DbInitializer.cs`               |   1 |   1 | إزالة كلمة المرور الثابتة لـ System Owner وربطها بمصدر seed ديناميكي.                                                                                                                                                                                                                  |
| `backend/KasserPro.Infrastructure/Data/MultiTenantSeeder.cs`           | 295 | 189 | إعادة هيكلة كبيرة: حصر tenants التجريبية في butcher + supermarket، إزالة tenants القديمة غير المطلوبة بشكل transactional مع حذف العلاقات التابعة، إضافة إغلاق الورديات المفتوحة للـ tenants المستهدفة، وإزالة تدفقات seed القديمة (home-appliances/restaurant).                        |
| `backend/KasserPro.Infrastructure/Data/RealisticDataSeeder.cs`         |  49 |   5 | حماية من إعادة بناء تدميرية عند وجود بيانات تشغيلية مرتبطة، وتعديل منطق وردية اليوم لتكون مغلقة مع حساب المجاميع المالية بشكل متسق.                                                                                                                                                    |
| `backend/KasserPro.Infrastructure/Data/SeedCatalogIconSynchronizer.cs` |  78 |  40 | مزامنة أيقونات بشكل deterministic على tenants المستهدفة فقط، إضافة mapping صريح لمنتجات الجزارة، وإزالة منطق الأيقونات العامة غير الدقيقة، مع تحسين قواعد اختيار الأيقونات.                                                                                                            |
| `backend/KasserPro.Infrastructure/Data/SeedInventorySynchronizer.cs`   |  19 |   4 | حصر المزامنة على tenants seed فقط، وتحسين حساب baseline/safety stock لتفادي كميات غير منطقية، وتحديث رسائل السجل.                                                                                                                                                                      |
| `backend/KasserPro.Infrastructure/Data/SqliteConfigurationService.cs`  |  21 |   6 | جعل `journal_mode` يعتمد على حالة التشفير: `WAL` لغير المشفر و`DELETE` للمشفر (SQLCipher compatibility)، مع تحقق/تحذير متوافقين مع الوضع المتوقع.                                                                                                                                      |
| `backend/KasserPro.Infrastructure/Data/SupermarketSeeder.cs`           |  46 |  23 | تحسين idempotency (عدم إعادة إنشاء cashiers/customers/categories إن كانت موجودة)، إضافة bootstrap guard لتفادي تعارض مع `RealisticDataSeeder`، وجعل الورديات كلها مغلقة مع تحديث المجاميع.                                                                                             |
| `backend/KasserPro.Infrastructure/KasserPro.Infrastructure.csproj`     |   9 |   7 | تحويل dependencies من `Microsoft.EntityFrameworkCore.Sqlite` إلى `Microsoft.EntityFrameworkCore.Sqlite.Core` وإضافة حزم SQLCipher (`provider/lib`).                                                                                                                                    |
| `backend/KasserPro.Infrastructure/Services/BackupService.cs`           |  62 |  13 | دعم النسخ الاحتياطي لقواعد البيانات المشفرة: fallback إلى file-copy بعد checkpoint عند عدم دعم SQLite Backup API للتشفير، وفحص integrity مع تمرير كلمة المرور.                                                                                                                         |
| `backend/KasserPro.Infrastructure/Services/DataValidationService.cs`   |  12 |   2 | جعل تحقق البيانات بعد الاستعادة يدعم DB مشفرة عبر parameter اختياري لكلمة المرور.                                                                                                                                                                                                      |
| `backend/KasserPro.Infrastructure/Services/RestoreService.cs`          |  18 |   8 | تمرير كلمة المرور من إعدادات الاتصال إلى integrity check وvalidation بعد restore، وتوحيد استخدام connection builder أثناء عملية الاسترجاع.                                                                                                                                             |
| `backend/KasserPro.Infrastructure/packages.lock.json`                  |  20 |  36 | مزامنة lockfile مع انتقال Infrastructure إلى SQLCipher وحذف مراجع bundle/provider الافتراضية القديمة.                                                                                                                                                                                  |
| `backend/KasserPro.Tests/packages.lock.json`                           |   3 |  19 | تحديث lockfile للاختبارات بما يتماشى مع تعديلات dependency graph.                                                                                                                                                                                                                      |

## تفاصيل الملفات الجديدة (Untracked)

| الملف                                                              | أسطر الملف | ملخص                                                                                                                                                           |
| ------------------------------------------------------------------ | ---------: | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `backend/KasserPro.Infrastructure/Data/SeedTenantRegistry.cs`      |         14 | ملف registry مركزي لتعريف slugs الخاصة بالـ seed tenants (`al-amana-butcher`, `supermarket`) مع helper للتحقق.                                                 |
| `backend/KasserPro.Tests/Integration/FreshInstallSeedDataTests.cs` |        308 | اختبارات تكامل جديدة تغطي سيناريو fresh install: التحقق من tenants المستهدفة فقط، صحة الأيقونات، اكتمال المخزون، عدم وجود ورديات مفتوحة، وحذف tenants القديمة. |
| `docs/337_VPS_REDEPLOY_RESET_DB_GUIDE.md`                          |         98 | دليل عربي عملي لإعادة نشر التطبيق على VPS مع مسح قاعدة البيانات القديمة مع backup وخطوات تحقق وrollback.                                                       |

## ملاحظات مهمة

- ظهرت تحذيرات CRLF/LF أثناء أوامر Git لبعض الملفات، ولم يتم تنفيذ reformat شامل ضمن هذه التعديلات.
- الملفات غير المتتبعة لا تظهر في `git diff --numstat` لذلك تم احتسابها بعدد أسطر الملف.
- هذا التقرير هو Snapshot للحالة الحالية قبل أي commit جديد.
