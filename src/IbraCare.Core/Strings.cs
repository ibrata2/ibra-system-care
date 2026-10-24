namespace IbraCare.Core;

/// <summary>Every visible text, Arabic and English. Keys are grouped by screen.</summary>
public static partial class Strings
{
    public static readonly Dictionary<string, (string Ar, string En)> Table = Build();

    private static Dictionary<string, (string Ar, string En)> Build()
    {
        var t = new Dictionary<string, (string Ar, string En)>();
        void A(string key, string ar, string en) => t[key] = (ar, en);

        // ---------- app ----------
        A("app.name", "IBRA SYSTEM CARE", "IBRA SYSTEM CARE");
        A("app.tagline", "صيانة وتنظيف وإصلاح Windows 11", "Windows 11 cleaning, care and repair");
        A("app.credit", "تصميم وتطوير وتعريب إبراهيم الخزرجي", "Designed, developed and localized by Ibrahim Al-Khazraji");
        A("app.developer", "المطوّر: إبراهيم الخزرجي", "Developer: Ibrahim Al-Khazraji");

        // ---------- errors ----------
        A("err.access", "لا توجد صلاحية للوصول. جرّب تشغيل البرنامج كمسؤول.", "Access denied. Try running the app as administrator.");
        A("err.inuse", "الملف مستخدم الآن من برنامج آخر.", "The file is being used by another program.");
        A("err.pathlong", "المسار طويل جداً.", "The path is too long.");
        A("err.notfound", "الملف أو المجلد غير موجود.", "The file or folder was not found.");
        A("err.network", "تعذّر الاتصال بالإنترنت أو بالخادم.", "Could not reach the internet or the server.");
        A("err.cancelled", "تم الإلغاء.", "Cancelled.");
        A("err.diskfull", "لا توجد مساحة كافية على القرص.", "There is not enough disk space.");
        A("err.generic", "حدث خطأ: {0}", "Something went wrong: {0}");

        // ---------- safety ----------
        A("safety.driveRoot", "رُفض: المسار هو جذر قرص كامل", "Refused: the path is a whole drive");
        A("safety.tooShallow", "رُفض: المسار قريب جداً من جذر القرص", "Refused: the path is too close to the drive root");
        A("safety.forbidden", "رُفض: مجلد نظام أو مجلد شخصي محمي", "Refused: a protected system or personal folder");
        A("safety.ownData", "رُفض: مجلد بيانات البرنامج نفسه", "Refused: the app's own data folder");

        // ---------- cleaner targets ----------
        A("target.userTemp", "الملفات المؤقتة للمستخدم", "User temporary files");
        A("target.userTemp.d", "ملفات تتركها البرامج في مجلد Temp. تُترك الملفات الحديثة لأنها قد تكون قيد الاستخدام.", "Files programs leave in the Temp folder. Recent files are left alone because they may be in use.");
        A("target.winTemp", "ملفات Windows المؤقتة", "Windows temporary files");
        A("target.winTemp.d", "مجلد Windows\\Temp. يحتاج صلاحيات المسؤول.", "The Windows\\Temp folder. Needs administrator rights.");
        A("target.wer", "تقارير أخطاء Windows", "Windows error reports");
        A("target.wer.d", "تقارير الأعطال التي جمعها Windows ولم تعد لازمة.", "Crash reports Windows collected that are no longer needed.");
        A("target.crashDumps", "ملفات تفريغ الأعطال", "Crash dumps");
        A("target.crashDumps.d", "ملفات ‎.dmp‎ تُنشأ عند تعطل البرامج. مفيدة للمطورين فقط.", "‎.dmp‎ files written when programs crash. Only useful to developers.");
        A("target.thumbCache", "ذاكرة الصور المصغرة", "Thumbnail cache");
        A("target.thumbCache.d", "يعيد Windows إنشاءها تلقائياً. الملفات المفتوحة من مستكشف الملفات تُتجاوز.", "Windows recreates it. Files held by File Explorer are skipped.");
        A("target.inetCache", "ذاكرة الإنترنت المؤقتة", "Internet cache");
        A("target.inetCache.d", "ذاكرة مؤقتة قديمة لمكونات الويب في Windows.", "Old cache of Windows web components.");
        A("target.updateCache", "ملفات تحديثات Windows المحمّلة", "Downloaded Windows Update files");
        A("target.updateCache.d", "ملفات تحديث تم تثبيتها. يُترك ما عمره أقل من 3 أيام. يحتاج صلاحيات المسؤول.", "Update files already installed. Anything newer than 3 days is kept. Needs administrator rights.");
        A("target.deliveryOpt", "ذاكرة تحسين التسليم", "Delivery Optimization cache");
        A("target.deliveryOpt.d", "نسخ التحديثات التي يشاركها Windows مع الأجهزة الأخرى.", "Update copies Windows shares with other devices.");
        A("target.winLogs", "سجلات Windows القديمة", "Old Windows logs");
        A("target.winLogs.d", "سجلات التثبيت والتحديث الأقدم من أسبوعين. يحتاج صلاحيات المسؤول.", "Setup and update logs older than two weeks. Needs administrator rights.");
        A("target.recycleBin", "سلة المحذوفات", "Recycle Bin");
        A("target.recycleBin.d", "تفريغ السلة حذف نهائي ولا يمكن التراجع عنه.", "Emptying the bin is permanent and cannot be undone.");
        A("target.chrome", "ذاكرة Google Chrome", "Google Chrome cache");
        A("target.chrome.d", "الكاش فقط — لا تُمس كلمات المرور ولا السجل ولا تسجيل الدخول.", "Cache only — passwords, history and sign-ins are not touched.");
        A("target.edge", "ذاكرة Microsoft Edge", "Microsoft Edge cache");
        A("target.edge.d", "الكاش فقط — لا تُمس كلمات المرور ولا السجل ولا تسجيل الدخول.", "Cache only — passwords, history and sign-ins are not touched.");
        A("target.brave", "ذاكرة Brave", "Brave cache");
        A("target.brave.d", "الكاش فقط — لا تُمس كلمات المرور ولا السجل ولا تسجيل الدخول.", "Cache only — passwords, history and sign-ins are not touched.");
        A("target.opera", "ذاكرة Opera", "Opera cache");
        A("target.opera.d", "الكاش فقط — لا تُمس كلمات المرور ولا السجل ولا تسجيل الدخول.", "Cache only — passwords, history and sign-ins are not touched.");
        A("target.firefox", "ذاكرة Firefox", "Firefox cache");
        A("target.firefox.d", "الكاش فقط — لا تُمس كلمات المرور ولا السجل ولا تسجيل الدخول.", "Cache only — passwords, history and sign-ins are not touched.");
        A("target.adobeMedia", "ذاكرة وسائط Adobe", "Adobe media cache");
        A("target.adobeMedia.d", "ملفات مؤقتة لبرامج Adobe للفيديو والصوت، يعاد إنشاؤها عند الحاجة.", "Temporary media files of Adobe video/audio apps, recreated when needed.");
        A("target.discord", "ذاكرة Discord", "Discord cache");
        A("target.discord.d", "صور ومقاطع مخزنة مؤقتاً.", "Cached images and clips.");
        A("target.teams", "ذاكرة Microsoft Teams", "Microsoft Teams cache");
        A("target.teams.d", "ذاكرة Teams الكلاسيكي المؤقتة.", "Classic Teams cache.");
        A("target.devCaches", "ذاكرة أدوات المطورين", "Developer tool caches");
        A("target.devCaches.d", "حزم npm و pip و Yarn المحفوظة. تُحمّل من جديد عند الحاجة.", "Saved npm, pip and Yarn packages. Downloaded again when needed.");
        A("target.shaderCache", "ذاكرة مظللات كارت الشاشة", "Graphics shader cache");
        A("target.shaderCache.d", "يعاد بناؤها تلقائياً؛ أول تشغيل للألعاب بعدها قد يكون أبطأ قليلاً.", "Rebuilt automatically; games may load a little slower the first time.");
        A("group.System", "Windows", "Windows");
        A("group.Browsers", "المتصفحات", "Browsers");
        A("group.Apps", "البرامج", "Apps");
        A("group.Graphics", "كارت الشاشة", "Graphics");

        // ---------- cleaner ----------
        A("clean.notAvailable", "غير متاح على هذا النظام", "Not available on this system");
        A("clean.needsAdmin", "يحتاج صلاحيات المسؤول", "Needs administrator rights");
        A("clean.backupTitle", "حجر المنظف — {0} ملف", "Cleaner quarantine — {0} files");
        A("clean.journalTitle", "تنظيف: {0} ملف — {1}", "Cleaning: {0} files — {1}");
        A("clean.modeQuarantineLine", "الطريقة: نقل إلى الحجر (يمكن التراجع)", "Mode: moved to quarantine (can be undone)");
        A("clean.modePermanentLine", "الطريقة: حذف نهائي", "Mode: permanent delete");
        A("clean.skippedHeader", "تم تجاوز {0} ملف:", "{0} files were skipped:");
        A("clean.outsideRoot", "خارج المجلد المسموح", "Outside the allowed folder");
        A("clean.goneAlready", "لم يعد موجوداً", "No longer exists");
        A("clean.isLink", "رابط رمزي — لا يُتبع", "A link — not followed");
        A("clean.isSystem", "ملف نظام", "A system file");
        A("clean.isKept", "في قائمة الاحتفاظ", "On the keep list");
        A("skip.TooNew", "حديثة", "Recent");
        A("skip.SystemFile", "ملفات نظام", "System files");
        A("skip.Link", "روابط", "Links");
        A("skip.Kept", "محتفظ بها", "Kept");
        A("skip.InUse", "مستخدمة", "In use");
        A("skip.NoAccess", "بلا صلاحية", "No access");
        A("skip.OwnData", "بيانات البرنامج", "App data");

        // ---------- repair center / backups ----------
        A("repair.backupMissing", "النسخة الاحتياطية غير موجودة", "The backup does not exist");
        A("repair.backupPurged", "حُذفت ملفات هذه النسخة الاحتياطية نهائياً", "This backup's files were deleted for good");
        A("repair.storedMissing", "الملف المحفوظ مفقود", "The saved copy is missing");
        A("repair.existsAgain", "يوجد ملف بنفس الاسم الآن، تُرك كما هو", "A file with that name exists again; left as is");
        A("repair.notUndoable", "هذه العملية لا تدعم التراجع", "This action cannot be undone");
        A("repair.undoTitle", "تراجع: {0}", "Undo: {0}");
        A("repair.undoCounts", "أُعيد {0}، تُرك {1}، فشل {2}", "Restored {0}, left {1}, failed {2}");
        A("repair.undone", "تم التراجع", "Undone");
        A("status.Info", "معلومة", "Info");
        A("status.Success", "نجح", "Success");
        A("status.Partial", "نجح جزئياً", "Partly done");
        A("status.Failed", "فشل", "Failed");
        A("sev.Error", "خطأ", "Error");
        A("sev.Warning", "تحذير", "Warning");
        A("sev.Info", "ملاحظة", "Note");
        A("restore.journal", "إنشاء نقطة استعادة للنظام", "Create a system restore point");
        A("restore.failedHint", "قد تكون حماية النظام متوقفة، أو أُنشئت نقطة خلال آخر 24 ساعة.", "System Protection may be off, or a point was already made in the last 24 hours.");

        // ---------- reports ----------
        A("report.title", "تقرير الصيانة", "Maintenance report");
        A("report.journal", "إنشاء تقرير", "Report created");
        A("report.place", "المكان", "Place");
        A("report.files", "الملفات", "Files");
        A("report.size", "الحجم", "Size");
        A("report.notes", "ملاحظات", "Notes");
        A("report.total", "المجموع", "Total");
        A("report.fontsSummary", "الخطوط: {0} ملف — تالف: {1} — مكرر: {2} — خطوط عربية سليمة: {3}", "Fonts: {0} files — damaged: {1} — duplicated: {2} — good Arabic fonts: {3}");
        A("report.font", "الخط", "Font");
        A("report.problem", "المشكلة", "Problem");
        A("report.path", "المسار", "Path");
        A("report.arabicFonts", "الخطوط التي تحتوي حروفاً عربية", "Fonts with Arabic letters");
        A("report.history", "السجل", "History");
        A("report.time", "الوقت", "Time");
        A("report.action", "العملية", "Action");
        A("report.result", "النتيجة", "Result");
        A("report.details", "التفاصيل", "Details");

        // ---------- fonts: problems found in files ----------
        A("font.p.tooSmall", "الملف صغير جداً ليكون خطاً", "The file is too small to be a font");
        A("font.p.tooBig", "الملف كبير بشكل غير طبيعي", "The file is unusually large");
        A("font.p.empty", "الملف فارغ (0 بايت)", "The file is empty (0 bytes)");
        A("font.p.woff", "خط ويب (WOFF) لا يُثبّت في Windows", "A web font (WOFF), not installable in Windows");
        A("font.p.badCollection", "مجموعة خطوط TTC تالفة", "Damaged TTC collection");
        A("font.p.truncated", "الملف مقطوع", "The file is cut short");
        A("font.p.badSignature", "توقيع الملف ليس خطاً صالحاً", "The file signature is not a valid font");
        A("font.p.badDirectory", "فهرس الجداول تالف", "The table directory is damaged");
        A("font.p.tableOutside", "جدول يشير خارج حدود الملف", "A table points outside the file");
        A("font.p.missingTable", "جدول أساسي ناقص ({0})", "A required table is missing ({0})");
        A("font.p.shortTable", "جدول أقصر من اللازم ({0})", "A table is too short ({0})");
        A("font.p.noOutlines", "لا توجد رسوم للحروف", "There are no glyph outlines");
        A("font.p.badHeadMagic", "ترويسة الخط (head) تالفة", "The font header (head) is damaged");
        A("font.p.badUnitsPerEm", "قيمة unitsPerEm غير صالحة", "Invalid unitsPerEm value");
        A("font.p.badNames", "جدول الأسماء تالف", "The name table is damaged");
        A("font.p.noFamilyName", "لا يوجد اسم للعائلة", "There is no family name");
        A("font.p.badCmap", "جدول ربط الحروف (cmap) تالف", "The character map (cmap) is damaged");
        A("font.p.emptyCmap", "لا يربط أي حرف برسم", "No character is mapped to a glyph");
        A("font.p.noGlyphs", "لا يحتوي أي حرف", "It contains no glyphs");
        A("font.p.damaged", "بيانات تالفة داخل الملف", "Damaged data inside the file");
        A("font.w.noPost", "جدول post ناقص", "The post table is missing");
        A("font.w.noOs2", "جدول OS/2 ناقص", "The OS/2 table is missing");
        A("font.w.checksum", "مجاميع تحقق غير مطابقة", "Checksums do not match");
        A("font.w.noWindowsNames", "لا يحتوي أسماء Windows (قد لا يظهر في القوائم)", "No Windows names (may not show in menus)");
        A("font.w.noUnicodeCmap", "لا يحتوي ربط Unicode", "No Unicode mapping");
        A("font.w.symbolEncoded", "ترميز رموز قديم بدل Unicode", "Old symbol encoding instead of Unicode");
        A("font.w.badLayout", "جداول التشكيل (GSUB/GPOS) تالفة", "Layout tables (GSUB/GPOS) are damaged");

        // ---------- fonts: issues ----------
        A("font.i.unreadable", "تعذّرت قراءة الملف: {0}", "The file could not be read: {0}");
        A("font.i.corrupt", "خط تالف: {0}", "Damaged font: {0}");
        A("font.i.type1", "خط Type 1 (PostScript): لم تعد إصدارات Photoshop الحديثة وكثير من البرامج تدعمه", "Type 1 (PostScript) font: no longer supported by current Photoshop and many apps");
        A("font.i.unregistered", "ملف موجود في مجلد خطوطك لكنه غير مسجل في Windows", "File is in your fonts folder but not registered with Windows");
        A("font.i.broken", "تسجيل في الريجستري ({0}) يشير إلى ملف غير موجود", "Registry entry ({0}) points to a missing file");
        A("font.i.legacyArabic", "خط عربي قديم بترميز رموز: الحروف تظهر مقلوبة أو منفصلة عند الكتابة بلوحة المفاتيح العربية", "Legacy symbol-encoded Arabic font: letters come out reversed or disconnected when typing with an Arabic keyboard");
        A("font.i.presentationOnly", "يعتمد على أشكال العرض القديمة فقط، بدون جداول تشكيل: الحروف قد تظهر منفصلة", "Relies only on old presentation forms, no shaping tables: letters may appear disconnected");
        A("font.i.noShaping", "فيه حروف عربية بلا قواعد وصل: ستظهر الحروف منفصلة", "Has Arabic letters but no joining rules: letters will appear disconnected");
        A("font.i.partialArabic", "دعم عربي ناقص ({0} من 36 حرفاً أساسياً، أو قواعد وصل ناقصة)", "Partial Arabic support ({0} of 36 core letters, or incomplete joining rules)");
        A("font.i.dupIdentical", "نسخة مكررة مطابقة تماماً لملف آخر", "An exact duplicate of another file");
        A("font.i.dupVersion", "نفس الخط مثبت مرتين بإصدارين مختلفين ({0} مقابل {1})", "The same font is installed twice in different versions ({0} vs {1})");
        A("font.allUsers", "لكل المستخدمين", "all users");
        A("font.thisUser", "لهذا المستخدم", "this user");
        A("arabic.None", "لا يوجد", "None");
        A("arabic.Full", "كامل — حروف متصلة", "Full — letters join");
        A("arabic.Partial", "جزئي", "Partial");
        A("arabic.PresentationFormsOnly", "أشكال عرض قديمة", "Old presentation forms");
        A("arabic.NoShaping", "بدون وصل", "No joining");
        A("arabic.LegacySymbol", "ترميز قديم", "Legacy encoding");

        // ---------- fonts: repairs ----------
        A("font.needAdmin", "هذا الخط مثبّت لكل المستخدمين؛ إزالته تحتاج تشغيل البرنامج كمسؤول.", "This font is installed for all users; removing it needs the app to run as administrator.");
        A("font.photoshopOpen", "تنبيه: Photoshop مفتوح؛ أغلقه وأعد فتحه بعد الإصلاح.", "Note: Photoshop is open; close and reopen it after the repair.");
        A("font.removeAfterRestart", "الملف مستخدم الآن؛ أُزيل تسجيله فلن يُحمَّل بعد إعادة التشغيل", "The file is in use; its registration was removed so it won't load after a restart");
        A("font.updated", "تم التحديث (النسخة القديمة محفوظة)", "Updated (the old copy is backed up)");
        A("font.licenseLine", "الرخصة: {0} — المصدر: {1}", "License: {0} — source: {1}");
        A("font.restartToFinish", "أعد تشغيل الكمبيوتر لإكمال إعادة بناء ذاكرة الخطوط.", "Restart the computer to finish rebuilding the font cache.");
        A("fix.corrupt.backup", "نسخة قبل إزالة {0} خط تالف", "Backup before removing {0} damaged fonts");
        A("fix.corrupt.journal", "إزالة {0} خط تالف", "Removed {0} damaged fonts");
        A("fix.dup.backup", "نسخة قبل إزالة {0} خط مكرر", "Backup before removing {0} duplicate fonts");
        A("fix.dup.journal", "إزالة {0} خط مكرر", "Removed {0} duplicate fonts");
        A("fix.broken.backup", "نسخة قبل حذف {0} تسجيل خط معطوب", "Backup before deleting {0} broken font entries");
        A("fix.broken.journal", "حذف {0} تسجيل خط معطوب", "Deleted {0} broken font entries");
        A("fix.install.backup", "نسخة قبل تثبيت {0}", "Backup before installing {0}");
        A("fix.install.journal", "تثبيت/تحديث الخط {0}", "Installed/updated font {0}");
        A("fix.pscache.backup", "نسخة ذاكرة خطوط {0}", "Font cache of {0}");
        A("fix.pscache.journal", "تفريغ ذاكرة خطوط {0}", "Cleared the font cache of {0}");
        A("fix.pscache.line", "نُقل {0} ملف من {1}", "Moved {0} files from {1}");
        A("fix.fontcache.backup", "نسخة ذاكرة خطوط Windows", "Windows font cache");
        A("fix.fontcache.journal", "إعادة بناء ذاكرة خطوط Windows", "Rebuilt the Windows font cache");

        // ---------- Google Fonts ----------
        A("gf.readingMeta", "قراءة بيانات الخط من Google Fonts…", "Reading the font's data from Google Fonts…");
        A("gf.checkingLicense", "التحقق من الرخصة…", "Checking the license…");
        A("gf.downloading", "تحميل {0}…", "Downloading {0}…");
        A("gf.noFiles", "لا توجد ملفات خط في المصدر", "The source lists no font files");
        A("gf.badLicense", "رخصة غير مقبولة: {0}", "License not accepted: {0}");
        A("gf.licenseMismatch", "ملف الرخصة لا يطابق الرخصة المعلنة — أُلغي التثبيت", "The license file does not match the declared license — cancelled");
        A("gf.badName", "اسم ملف غير صالح في المصدر", "Invalid file name in the source");
        A("gf.invalidFont", "الملف المحمّل ليس خطاً سليماً: {0}", "The downloaded file is not a valid font: {0}");
        A("gf.restricted", "الخط يمنع التضمين، لن يُثبّت: {0}", "The font restricts embedding, not installed: {0}");
        A("gf.http", "رد الخادم برمز {0}", "The server answered {0}");

        // ---------- Photoshop ----------
        A("ps.f.corrupt", "خط تالف — قد يجمّد قائمة الخطوط أو يغلق Photoshop", "Damaged font — can freeze the font menu or crash Photoshop");
        A("ps.f.type1", "خط Type 1 لا يظهر في Photoshop الحديث — استبدله بنسخة OpenType", "Type 1 font won't show in current Photoshop — replace it with an OpenType version");
        A("ps.f.dup", "مثبّت مرتين — قد يستخدم Photoshop النسخة الخطأ", "Installed twice — Photoshop may use the wrong copy");
        A("ps.f.legacy", "الحروف ستظهر مقلوبة أو مقطعة في Photoshop", "Letters will come out reversed or broken in Photoshop");
        A("ps.f.disconnected", "الحروف العربية ستظهر منفصلة في Photoshop", "Arabic letters will appear disconnected in Photoshop");
        A("ps.f.names", "لا يحتوي أسماء Windows؛ قد يظهر باسم آخر أو لا يظهر", "No Windows names; may show under another name or not at all");
        A("ps.f.broken", "تسجيل خط معطوب يبطئ تحميل الخطوط", "Broken font entry slows down font loading");
        A("ps.f.many", "عدد الخطوط كبير ({0}) — قائمة الخطوط ستكون بطيئة", "Very many fonts ({0}) — the font menu will be slow");
        A("ps.f.cache", "ذاكرة خطوط Photoshop ({0}) — تفريغها يحل ظهور خطوط قديمة أو ناقصة", "Photoshop font cache ({0}) — clearing it fixes missing or stale fonts");
        A("ps.a.notFound", "لم يُعثر على Photoshop في المجلدات المعتادة؛ الفحص يشمل الخطوط فقط.", "Photoshop was not found in the usual folders; only fonts are checked.");
        A("ps.a.worldReady", "في Photoshop: Edit ← Preferences ← Type ← اختر World-Ready Layout (أو Middle Eastern and South Asian في الإصدارات الأقدم)، ثم أعد تشغيل Photoshop.", "In Photoshop: Edit → Preferences → Type → choose World-Ready Layout (Middle Eastern and South Asian in older versions), then restart Photoshop.");
        A("ps.a.composer", "من لوحة Paragraph ← القائمة ← اختر World-Ready Paragraph Composer.", "In the Paragraph panel menu, choose World-Ready Paragraph Composer.");
        A("ps.a.direction", "اضبط اتجاه الفقرة من اليمين لليسار (Right-to-Left) من لوحة Paragraph.", "Set the paragraph direction to Right-to-Left in the Paragraph panel.");
        A("ps.a.restart", "بعد أي تثبيت أو إزالة للخطوط: أغلق Photoshop بالكامل ثم افتحه.", "After installing or removing fonts: quit Photoshop completely and open it again.");
        A("ps.a.prefs", "إذا بقيت المشكلة: افتح Photoshop مع الضغط على Ctrl+Alt+Shift لإعادة تعيين التفضيلات.", "If the problem stays: start Photoshop holding Ctrl+Alt+Shift to reset its preferences.");
        A("ps.adviceTitle", "خطوات داخل Photoshop", "Steps inside Photoshop");
        A("ps.noCache", "لا توجد ذاكرة خطوط لهذا الإصدار", "There is no font cache for this version");
        A("ps.closeFirst", "أغلق Photoshop أولاً ثم أعد المحاولة.", "Close Photoshop first, then try again.");

        // ---------- old Arabic fonts (FS_ and others) ----------
        A("font.i.legacyConvertible", "خط عربي قديم بترميز الرموز (مثل خطوط FS_): الحروف لا تظهر أو تظهر منفصلة في Photoshop. يمكن تحويله إلى Unicode لتتصل حروفه مع الحفاظ على شكله واسمه.", "Old symbol-encoded Arabic font (like the FS_ fonts): letters don't show or come out disconnected in Photoshop. It can be converted to Unicode so its letters join, keeping its look and name.");
        A("ps.f.convertible", "خط عربي قديم — يحتاج تحويلاً إلى Unicode حتى يشتغل في Photoshop الحديث", "Old Arabic font — needs converting to Unicode to work in current Photoshop");
        A("fix.ConvertLegacy", "تحويل إلى Unicode", "Convert to Unicode");
        A("fix.legacy.backup", "نسخة قبل تحويل {0} خط عربي قديم", "Backup before converting {0} old Arabic fonts");
        A("fix.legacy.journal", "تحويل {0} خط عربي قديم إلى Unicode", "Converted {0} old Arabic fonts to Unicode");
        A("fix.legacy.line", "{0} حرفاً، {1} شكل اتصال، {2} لام ألف", "{0} letters, {1} joining forms, {2} lam-alef");
        A("legacy.notTrueType", "ليس خط TrueType قابلاً للتحويل", "Not a convertible TrueType font");
        A("legacy.hasLayout", "الخط يحتوي جداول تشكيل مسبقاً", "The font already has layout tables");
        A("legacy.noNames", "الخط لا يحتوي أسماء للحروف", "The font has no glyph names");
        A("legacy.notArabicNames", "أسماء الحروف لا تطابق نمط الخطوط العربية القديمة", "Glyph names don't follow the old Arabic font scheme");
        A("legacy.notSymbol", "الخط مرمز بـ Unicode أصلاً", "The font is already Unicode-encoded");
        A("legacy.damaged", "ملف الخط تالف", "The font file is damaged");

        A("pack.journal", "حزمة خطوط لحاسبة ثانية: {0} خط", "Font pack for another PC: {0} fonts");
        AddUi(A);
        return t;
    }

    /// <summary>Texts of the Windows app, in StringsUi.cs.</summary>
    static partial void AddUiTexts(Action<string, string, string> A);

    private static void AddUi(Action<string, string, string> add) => AddUiTexts(add);
}
