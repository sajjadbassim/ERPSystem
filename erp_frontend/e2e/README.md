# اختبارات المتصفح الحيّة — `e2e/`

| الملف | البادئة | القاعدة | يكتب؟ |
|---|---|---|---|
| `accounts.spec.ts` | `BR` | أيّ قاعدة تطوير | حسابان بالإدراج وحده (`global-setup.ts`) |
| `journal-entry.spec.ts` | `BJE` | **`ErpApi_E2E` وحدها** | **قيود مرحَّلة دائمة، وقفل عملة الأساس** |

## ⛔ `BJE` لا يُشغَّل على قاعدة التطوير `ErpApi`

الترحيل الناجح يكتب قيداً لا يُحذف (R-GL-03) ويقفل عملة أساس الشركة نهائياً
(R-BASE-02). فالهدف قاعدة قابلة للتخلّص: `ErpApi_E2E` على `(localdb)\MSSQLLocalDB`.

**وحارسان في `journal-entry.spec.ts` يسبقان أي كتابة:**
1. `ERP_E2E_DATABASE=ErpApi_E2E` في بيئة Playwright — إعلان.
2. حساب العلامة `E2E-MARKER` في القاعدة التي يتّصل بها الخادم — **بنيويّ**: لا يوجد في
   `ErpApi`، فإن كان الخادم عليها يرسب الاختبار قبل أول كتابة **من `BJE`**.
   ⚠ **مقيس: الدخول نفسه يسبق الحارس ويكتب رمز تجديد** — قراءة العلامة تحتاج رمزاً،
   فلا مفرّ منه. ولذلك يبقى الحارس الأول والتحقق في الخطوة ٣ هما الوقاية من الدخول
   على القاعدة الخطأ.

## التشغيل — بالترتيب

### ١. القاعدة (مرة واحدة — **بيد المطوّر**)

إنشاء `ErpApi_E2E` وتطبيق الترحيلات عليها قرارٌ بشريّ بنصّ `CLAUDE.md` («تطبيق
Migration تلقائياً ممنوع»)، فلا أمر له هنا.

### ٢. تشغيل ErpApi على قاعدة E2E — طرفية مستقلة، ويبقى يعمل

سلسلة الاتصال **متغيّر بيئة لجلسة الطرفية وحدها** — يغلب User Secrets، ولا يمسّ
`appsettings` ولا الأسرار.

PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=ErpApi_E2E;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
cd erp_backend
dotnet run --project ErpApi --launch-profile https
```

Bash:

```bash
export ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=ErpApi_E2E;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
cd erp_backend && dotnet run --project ErpApi --launch-profile https
```

### ٣. التحقق بالقياس — قبل أي كتابة

دخولٌ باسم **غير موجود** يقرأ ولا يكتب (`AuthService.cs:40-49`)، فيفتح الخادم اتصالاً
بقاعدته دون أثر. ثم يُسأل SQL Server أيّ قاعدة يحملها اتصال العملية:

```powershell
curl.exe -k -s -o NUL -w "%{http_code}" -X POST https://localhost:7175/api/auth/login -H "Content-Type: application/json" --data '{\"userName\":\"e2e-probe-no-such-user\",\"password\":\"x\"}'
$pid7175 = (Get-NetTCPConnection -LocalPort 7175 -State Listen).OwningProcess | Select-Object -First 1
sqlcmd -E -S "(localdb)\MSSQLLocalDB" -W -Q "SELECT DISTINCT DB_NAME(database_id) FROM sys.dm_exec_sessions WHERE host_process_id = $pid7175;"
```

المتوقَّع `401` ثم `ErpApi_E2E`. **وأيّ نتيجة أخرى توقِف كل ما بعدها.**

> ⚠ لا تُستعمل `devadmin` للتحقق: إن لم يسرِ المتغيّر، ينجح الدخول على `ErpApi`
> فيكتب فيها رمز تجديد.

### ٤. البذر (مرة واحدة لكل قاعدة E2E جديدة)

في طرفية فيها **سلسلة الاتصال نفسها**:

```powershell
$env:ConnectionStrings__DefaultConnection = '<السلسلة نفسها أعلاه>'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
cd erp_backend
dotnet run --project ErpApi.DevSeedTool
```

ثم **حساب العلامة** — يدوياً، **بعد** التحقق في الخطوة ٣ وحدها:
`POST /api/accounts` بـ`code: "E2E-MARKER"` و`isPostable: false` و`accountType: 1`
و`normalBalance: 0`. وجوده في القاعدة هو ما يأذن لـ`BJE` بالكتابة.

والباقي يبذره `BJE` نفسه بالإدراج وحده: USD، وسنة مالية بفترة عادية تغطي السنة،
والحسابات `1110` و`1100` و`1200`.

### ٥. الاختبارات

```powershell
cd erp_frontend
$env:ERP_E2E_DATABASE = 'ErpApi_E2E'
npx playwright test
```

## ما يتراكم بين التشغيلات

كل تشغيلة لـ`BJE` تُرحّل قيدين ناجحين (`BJE01` و`BJE03`)، والرفضان لا يكتبان.
**القاعدة تكبر ولا تُنظَّف** — وهي قابلة للتخلّص بحذفها وإعادة الخطوات ١ و٤.
