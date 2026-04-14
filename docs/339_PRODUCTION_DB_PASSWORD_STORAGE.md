# سياسة تخزين كلمة مرور قاعدة بيانات الإنتاج

## القاعدة الأمنية

- ممنوع تخزين كلمة مرور تشفير قاعدة البيانات داخل أي ملف config يتم عمل commit له.
- المسموح فقط في الإنتاج:
  - `Environment Variable`.
  - `Windows DPAPI` (ملف مشفر محلي على السيرفر).

## مصادر كلمة المرور المعتمدة في الكود

الترتيب الحالي في التشغيل:

1. `Connection String Password` (لو تم حقنه وقت التشغيل فقط، وليس داخل ملف committed).
2. متغير البيئة `KASSERPRO_DB_PASSWORD`.
3. ملف DPAPI على ويندوز:
   - مسار افتراضي: `C:\ProgramData\KasserPro\secrets\db-password.dpapi`
   - أو مسار مخصص عبر `KASSERPRO_DB_PASSWORD_DPAPI_FILE`.
4. متغير قديم للتوافق: `KASSERPRO_DB_ENCRYPTION_PASSWORD` (يفضل إيقافه لاحقاً).

## خيار 1: Environment Variable

على ويندوز (Machine scope):

```powershell
[Environment]::SetEnvironmentVariable(
  "KASSERPRO_DB_PASSWORD",
  "<STRONG_RANDOM_PASSWORD>",
  "Machine"
)
```

## خيار 2: Windows DPAPI (موصى به للأجهزة المحلية)

```powershell
$plain = "<STRONG_RANDOM_PASSWORD>"
$bytes = [Text.Encoding]::UTF8.GetBytes($plain)
$enc = [Security.Cryptography.ProtectedData]::Protect(
  $bytes,
  $null,
  [Security.Cryptography.DataProtectionScope]::LocalMachine
)
$base64 = [Convert]::ToBase64String($enc)

$secretDir = "C:\ProgramData\KasserPro\secrets"
$secretFile = Join-Path $secretDir "db-password.dpapi"

New-Item -ItemType Directory -Force -Path $secretDir | Out-Null
Set-Content -Path $secretFile -Value $base64 -Encoding ascii

[Environment]::SetEnvironmentVariable(
  "KASSERPRO_DB_PASSWORD_DPAPI_FILE",
  $secretFile,
  "Machine"
)
```

## قواعد تشغيل مهمة

- لا تضف `DatabaseEncryption:Password` داخل `appsettings.json` أو `appsettings.*.json` المتتبعة في Git.
- لا تضع كلمة المرور في أي وثيقة داخل المستودع.
- بعد تحديث المتغيرات، أعد تشغيل خدمة التطبيق لالتقاط القيم الجديدة.
