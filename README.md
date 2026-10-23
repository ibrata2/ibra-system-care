<p align="center"><img src="assets/ibracare-256.png" width="120" alt="IBRA SYSTEM CARE"></p>

# IBRA SYSTEM CARE

برنامج صيانة لـ Windows 11 بواجهة عربية (من اليمين لليسار) وإنجليزية كاملة —
**تصميم وتطوير وتعريب إبراهيم الخزرجي**

## التحميل
آخر إصدار: **[IBRA-System-Care-Setup.exe](https://github.com/ibrata2/ibra-system-care/releases/latest/download/IBRA-System-Care-Setup.exe)**
أو النسخة المحمولة: **[IBRA-System-Care-Portable-x64.zip](https://github.com/ibrata2/ibra-system-care/releases/latest/download/IBRA-System-Care-Portable-x64.zip)**
(إذا ظهر تحذير Windows: More info ← Run anyway)

## الأقسام

### IBRA Deep Cleaner — المنظف العميق
- يفحص 20 مكاناً معروفاً: ملفات Temp للمستخدم و Windows، تقارير الأعطال، ملفات التفريغ، ذاكرة الصور المصغرة، ذاكرة الإنترنت، ملفات تحديثات Windows، تحسين التسليم، سجلات Windows القديمة، سلة المحذوفات، كاش Chrome / Edge / Brave / Opera / Firefox (الكاش فقط، بدون كلمات المرور أو السجل)، Adobe و Discord و Teams، أدوات المطورين، ومظللات كارت الشاشة.
- يعرض **المسار والحجم الحقيقي** لكل ملف، ومعاينة كاملة قبل الحذف مع إمكانية إلغاء أي ملف أو إضافته لقائمة «احتفظ دائماً».
- **يستثني تلقائياً:** ملفات النظام، الملفات المستخدمة من برامج أخرى، الروابط الرمزية (لا يتبعها أبداً)، الملفات الحديثة، وأي مجلد نظام أو شخصي محمي — حتى لو كان متغير %TEMP% مضبوطاً بشكل خاطئ.
- طريقتان: **نقل إلى الحجر** (افتراضي، قابل للتراجع) أو حذف نهائي.

### IBRA Font Doctor — طبيب الخطوط
- يقرأ كل الخطوط المثبتة (لكل المستخدمين ولحسابك) بمحلل OpenType مكتوب داخل البرنامج: يكشف **الخطوط التالفة**، **المكررة** (ويقترح النسخة الأفضل للإبقاء)، **تسجيلات الريجستري المعطوبة**، وخطوط Type 1.
- يفحص **دعم العربية** (36 حرفاً + گ چ پ ژ ڤ + التشكيل + الأرقام) و**الإنجليزية**، ويتحقق من **قواعد الوصل** (init/medi/fina) ومن الخطوط القديمة بترميز الرموز التي تظهر حروفها مقلوبة.
- **اختبار حي** للاتصال والاتجاه بالخط نفسه، مع معاينة نص قابل للتعديل.
- **تشخيص Photoshop** الحديث: الإصدارات المثبتة، ذاكرة خطوطه (CT Font Cache) وتفريغها، الخطوط التي تسبب التجمّد أو تظهر منفصلة، وخطوات World-Ready Layout.
- **بدائل متوافقة** و**خطوط موثوقة**: 32 خطاً عربياً من مستودع Google Fonts الرسمي، مع التحقق من الرخصة (OFL) وسلامة كل ملف قبل التثبيت، والتثبيت لحسابك بدون صلاحيات مسؤول، والبحث عن تحديثات.
- **نسخة احتياطية قبل كل إصلاح** (الملفات وسطور الريجستري).

### IBRA Repair Center — مركز الإصلاح
- سجل لكل فحص وإصلاح مع التفاصيل.
- النسخ الاحتياطية: استعادة، حذف نهائي، حذف المنتهي تلقائياً.
- **التراجع** عن التنظيف وإصلاحات الخطوط وتفريغ ذاكرة Photoshop.
- تقارير HTML عربية/إنجليزية، ونقطة استعادة للنظام.

## البنية
| المجلد | المحتوى |
|---|---|
| `src/IbraCare.Core` | المحرك (net10.0، بدون أي حزمة خارجية): المنظف، طبيب الخطوط، مركز الإصلاح، التعريب |
| `src/IbraCare.App` | واجهة Windows (WPF مبنية بالكود، عربي RTL / إنجليزي) |
| `tests/` | 128 اختباراً حقيقياً: ملفات وروابط ومجلدات وهمية لـ Windows، خطوط حقيقية وتالفة، ريجستري في الذاكرة، تحميل من Google Fonts |
| `installer/` | مثبّت Inno Setup (عربي + إنجليزي) |
| `tools/` | مولّد الأيقونات، وفحص ترجمة كود WPF على Linux |
| `docs/ANALYSIS.md` | تحليل المشروعين السابقين وقرار البنية |

## البناء
**تلقائياً:** كل دفع (push) إلى `main` يشغّل الاختبارات، ثم يبني البرنامج على Windows حقيقي، ويشغّل **فحصاً ذاتياً** على Windows، ويلتقط صوراً لكل الصفحات، ويبني المثبّت، وينشر إصداراً جديداً.

**على جهازك (Windows):** ثبّت [.NET 10 SDK](https://dotnet.microsoft.com/download) و [Inno Setup 6](https://jrsoftware.org/isdl.php) ثم:
```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD-ON-WINDOWS.ps1 -Version 1.0.0
```

**الاختبارات (Linux أو Windows):**
```bash
tests/run.sh            # كل الاختبارات
tests/run.sh --offline  # بدون تحميل من الإنترنت
tools/typecheck.sh      # يتأكد أن كود الواجهة يُترجم (على Linux)
```

**أوامر خاصة للبرنامج:** `--selftest <ملف>` فحص ذاتي يقرأ فقط · `--snapshot <مجلد> --theme dark --lang ar` صور للصفحات · `--page fonts` يفتح صفحة معينة · `--lang en`.

## الأمان
- يعمل كمستخدم عادي؛ الأجزاء التي تحتاج صلاحيات (Windows\Temp، خطوط كل المستخدمين، ذاكرة خطوط Windows) تعرض زر «إعادة التشغيل كمسؤول».
- لا حذف ولا إصلاح بدون تأكيد يعرض بالضبط ما سيتغير.
- بيانات البرنامج: `%LOCALAPPDATA%\IBRA System Care` (الإعدادات، السجل، النسخ الاحتياطية)، والتقارير في `المستندات\IBRA System Care\Reports`.

---

## English

IBRA SYSTEM CARE is a Windows 11 maintenance app — Arabic by default (right to left) with a complete English interface.
Designed, developed and localized by **Ibrahim Al-Khazraji**.

- **IBRA Deep Cleaner** — scans 20 known places (temp files, error reports, crash dumps, thumbnail and browser caches, update files, logs, Recycle Bin…), shows real paths and sizes, lets you untick or always-keep any file, skips system files, files in use, links and protected folders, and moves files to an undoable quarantine (or deletes permanently if you choose).
- **IBRA Font Doctor** — reads every installed font with a built-in OpenType parser: damaged and duplicate fonts, broken registry entries, Type 1 fonts, Arabic and English coverage, Arabic joining rules, legacy symbol-encoded Arabic fonts, a live joining/direction test, Photoshop diagnostics (font cache, World-Ready Layout), compatible alternatives, and verified installs/updates from the official Google Fonts repository. Every repair is backed up first.
- **IBRA Repair Center** — history of every scan and repair, backups, undo, HTML reports, System Restore point.

Build: push to `main` (GitHub Actions builds, self-tests on Windows, makes the installer and a release) or run `BUILD-ON-WINDOWS.ps1`.
