# تحليل المشروعين السابقين وقرار البنية — IBRA SYSTEM CARE

> تم الفحص على نسخة فعلية من المستودعين (git clone بتاريخ 2026-10-23). كل ما هنا مأخوذ من الملفات نفسها.

## 1) ibrata2/ibra-adawat

| البند | النتيجة |
|---|---|
| الأصل | نسخة مطوّرة (fork) من `motalim-ai/adawat` |
| اللغات | Swift (macOS) + C# (Windows و Linux) + Kotlin (Android) |
| إطار Windows | .NET 10، **WPF** (`net10.0-windows10.0.22621.0`)، واجهة مبنية بالكود، أيقونة النظام عبر `H.NotifyIcon.Wpf` |
| البنية (Windows) | `windows/src/Adawat.Core` (العمليات، متعدد الأهداف net10.0 + windows) · `Adawat.App` (الواجهة، العجلة، زر يمين Explorer) · `Adawat.Cli` · `windows/shared/strings.ar.json` (الترجمة) · `windows/installer/Adawat.iss` |
| الاعتماديات | Magick.NET، PDFsharp، PdfPig، OpenXml، ClosedXML، SharpCompress، QRCoder، ZXing، ONNX Runtime، WebView2، PDFtoImage + أدوات خارجية: ffmpeg، whisper.cpp، yt-dlp، Real-ESRGAN |
| التشغيل والبناء | `dotnet publish` لـ win-x64 self-contained ثم تجميع الأدوات في `tools/` |
| الإصدارات | GitHub Actions (`windows-latest`) ← Inno Setup بالعربي ← `IBRA-Adawat-Setup-x64.exe` ← إصدار `v1.1.N` تلقائي |
| الترخيص | **GPL v3** (موروث من Adawat). أي كود يُنسخ منه يجعل المشروع الجديد GPL v3 بالكامل |
| حجم كود Windows | ≈ 14,600 سطر C# |

**ما يفيد المشروع الجديد:** فكرة جدول الترجمة الخارجي (مفتاح ← نص عربي)، طريقة التعامل مع RTL، أسلوب تجميع المثبّت والإصدار، وخطوط Noto Sans Arabic UI (رخصة OFL مستقلة).
**ما لم يُنقل ولماذا:** لم يُنسخ أي كود C# من Adawat حتى لا يُفرض ترخيص GPL v3 على IBRA SYSTEM CARE، ولأن وحداته (صور/فيديو/PDF) خارج مجال الصيانة. أُعيد بناء البديل (نظام التعريب ثنائي اللغة) من الصفر.

## 2) ibrata2/ibdm-pro

| البند | النتيجة |
|---|---|
| اللغة والإطار | C# / .NET 10. الواجهة **WPF** مبنية بالكود بالكامل (بدون XAML)، WinForms فقط لأيقونة الإشعارات |
| البنية | `src/IbdmPro.Core` (محرك بدون واجهة، `net10.0` يُختبر على Linux) · `src/IbdmPro.App` (واجهة Windows عربية RTL) · `tests/` (اختبارات حقيقية + خادم Node) · `extension/` (إضافة متصفح) · `installer/IbdmPro.iss` · `tools/make-icons.py` |
| الاعتماديات | لا حزم NuGet إطلاقاً. أدوات خارجية: yt-dlp و FFmpeg |
| التشغيل والبناء | `dotnet publish src/IbdmPro.App -r win-x64 --self-contained` |
| الإصدارات | Actions: اختبارات على Ubuntu ← بناء Windows ← فحص تشغيل البرنامج ← لقطات للنوافذ ← مثبّت Inno بالعربي ← إصدار `v1.0.N` |
| التحديث الذاتي | `Updater.cs` يقرأ GitHub Releases ويثبّت التحديث بصمت |
| الترخيص | **لا يوجد ملف ترخيص** في المستودع ← الكود ملك صاحبه (إبراهيم الخزرجي)، ويحق له إعادة استخدامه |
| حجم الكود | ≈ 4,500 سطر C# |

**ما أُعيد استخدامه (بعد التكييف):** نظام الألوان والأنماط المبنية بالكود (`Theme`/`Ui`)، شريط العنوان الداكن، عرض النصوص الإنجليزية والمسارات LTR داخل واجهة RTL، الحفظ الذري لملفات JSON، السجل، مسارات البرنامج، تنسيق الأحجام، آلية التحديث من GitHub، لقطات النوافذ في CI، بنية مثبّت Inno Setup وسير عمل Actions، ومولّد الأيقونات بـ Python.

## 3) المقارنة والقرار

| المعيار | ibra-adawat | ibdm-pro |
|---|---|---|
| الترخيص | GPL v3 (قيود) | ملكك (حر) |
| الاعتماديات | ثقيلة (12 حزمة + 4 أدوات) | صفر حزم |
| قابلية الاختبار | محدودة | محرك منفصل يُختبر على Linux |
| مناسبة لبرنامج صيانة | بعيدة | قريبة جداً |

**القرار:** IBRA SYSTEM CARE مشروع مستقل مبني على **بنية IBDM Pro**:

- `IbraCare.Core` — `net10.0` بدون أي حزمة خارجية: المنظف، طبيب الخطوط (محلّل OpenType مكتوب من الصفر)، مركز الإصلاح، التعريب.
- `IbraCare.App` — WPF مبني بالكود، عربي RTL افتراضياً + إنجليزي كامل.
- `tests/IbraCare.Tests` — اختبارات حقيقية على ملفات وخطوط فعلية.
- مثبّت Inno Setup + GitHub Actions مع فحص ذاتي على Windows حقيقي.

لم تُعدَّل أي ملفات في المستودعين الأصليين ولم يُنشر عليهما شيء.
