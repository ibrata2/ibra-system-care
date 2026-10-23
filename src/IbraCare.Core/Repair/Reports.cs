using System.Net;
using System.Text;
using IbraCare.Core.Cleaner;
using IbraCare.Core.Fonts;

namespace IbraCare.Core.Repair;

/// <summary>A readable HTML report (opens in any browser, prints well): last scans, problems found and the history.</summary>
public static class Reports
{
    public static string Write(ScanReport? clean, FontScan? fonts, PhotoshopReport? ps, string? folder = null)
    {
        var file = Path.Combine(folder ?? Paths.Reports, $"IBRA-System-Care-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html");
        File.WriteAllText(file, Build(clean, fonts, ps), new UTF8Encoding(false));
        Journal.Add("repair", EntryStatus.Info, "report.journal", [file]);
        return file;
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Build(ScanReport? clean, FontScan? fonts, PhotoshopReport? ps)
    {
        var dir = L.IsRtl ? "rtl" : "ltr";
        var sb = new StringBuilder();
        sb.Append($$"""
        <!doctype html><html lang="{{L.Language}}" dir="{{dir}}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>IBRA SYSTEM CARE — {{H(L.T("report.title"))}}</title>
        <style>
        body{font-family:"Segoe UI",Tahoma,Arial,sans-serif;background:#111114;color:#eee;margin:0;padding:28px;line-height:1.6}
        h1{color:#D4AF37;margin:0 0 4px}h2{color:#D4AF37;border-bottom:1px solid #333;padding-bottom:6px;margin-top:32px}
        .sub{color:#999}table{border-collapse:collapse;width:100%;margin:10px 0}td,th{border-bottom:1px solid #2a2a2f;padding:6px 8px;text-align:start;vertical-align:top}
        th{color:#aaa;font-weight:600}.path{direction:ltr;unicode-bidi:isolate;font-family:Consolas,monospace;font-size:12px;color:#bbb;word-break:break-all}
        .num{direction:ltr;unicode-bidi:isolate}.err{color:#ff7a66}.warn{color:#f0c040}.ok{color:#5fd07a}.info{color:#7ab8ff}
        @media print{body{background:#fff;color:#000}h1,h2{color:#8a6a00}.path{color:#333} }
        </style></head><body>
        <h1>IBRA SYSTEM CARE</h1>
        <div class="sub">{{H(L.T("report.title"))}} — <span class="num">{{Fmt.Date(DateTime.Now)}}</span> — {{H(Environment.MachineName)}}</div>
        <div class="sub">{{H(L.T("app.credit"))}}</div>
        """);

        if (clean is not null)
        {
            sb.Append($"<h2>{H(L.T("nav.cleaner"))}</h2><table><tr><th>{H(L.T("report.place"))}</th><th>{H(L.T("report.files"))}</th><th>{H(L.T("report.size"))}</th><th>{H(L.T("report.notes"))}</th></tr>");
            foreach (var t in clean.Targets)
            {
                var notes = string.Join(L.Sep, t.Notes.Concat(t.Skipped.Select(kv => $"{L.T("skip." + kv.Key)}: {kv.Value}")).Concat(t.NeedsAdmin ? [L.T("clean.needsAdmin")] : []));
                sb.Append($"<tr><td>{H(t.Target.Name)}</td><td class=num>{Fmt.Count(t.Count)}</td><td class=num>{Fmt.Size(t.Size)}</td><td>{H(notes)}</td></tr>");
            }
            sb.Append($"<tr><th>{H(L.T("report.total"))}</th><th class=num>{Fmt.Count(clean.TotalCount)}</th><th class=num>{Fmt.Size(clean.TotalSize)}</th><th></th></tr></table>");
        }

        if (fonts is not null)
        {
            sb.Append($"<h2>{H(L.T("nav.fonts"))}</h2>");
            sb.Append($"<p>{H(L.T("report.fontsSummary", Fmt.Count(fonts.Files.Count), Fmt.Count(fonts.CorruptCount), Fmt.Count(fonts.DuplicateGroups), Fmt.Count(fonts.ArabicReady.Count())))}</p>");
            if (fonts.Issues.Count > 0)
            {
                sb.Append($"<table><tr><th></th><th>{H(L.T("report.font"))}</th><th>{H(L.T("report.problem"))}</th><th>{H(L.T("report.path"))}</th></tr>");
                foreach (var i in fonts.Issues.OrderBy(i => i.Severity))
                    sb.Append($"<tr><td class={Css(i.Severity)}>{H(L.T("sev." + i.Severity))}</td><td>{H(i.FontName)}</td><td>{H(i.Message)}</td><td class=path>{H(i.Path)}</td></tr>");
                sb.Append("</table>");
            }
            sb.Append($"<h3>{H(L.T("report.arabicFonts"))}</h3><table><tr><th>{H(L.T("report.font"))}</th><th>{H(L.T("fonts.colArabic"))}</th><th>{H(L.T("fonts.colLatin"))}</th><th>{H(L.T("fonts.colExtra"))}</th></tr>");
            foreach (var f in fonts.Faces.Where(f => f.Arabic != ArabicSupport.None).OrderBy(f => f.Name))
                sb.Append($"<tr><td>{H(f.Name)}</td><td>{H(FontInventory.ArabicLabel(f.Arabic))}</td><td>{(f.Latin ? "✓" : "—")}</td><td>{(f.ArabicExtended ? "گ چ پ ژ ✓" : "—")}</td></tr>");
            sb.Append("</table>");
        }

        if (ps is not null)
        {
            sb.Append($"<h2>Photoshop</h2>");
            foreach (var i in ps.Installs) sb.Append($"<p>{H(i.Name)} <span class=num>{H(i.Version)}</span></p>");
            sb.Append("<ul>");
            foreach (var f in ps.Findings) sb.Append($"<li class={Css(f.Severity)}>{H(f.FontName)} — {H(f.Message)}</li>");
            sb.Append("</ul><h3>").Append(H(L.T("ps.adviceTitle"))).Append("</h3><ol>");
            foreach (var a in ps.Advice) sb.Append($"<li>{H(a)}</li>");
            sb.Append("</ol>");
        }

        sb.Append($"<h2>{H(L.T("report.history"))}</h2><table><tr><th>{H(L.T("report.time"))}</th><th>{H(L.T("report.action"))}</th><th>{H(L.T("report.result"))}</th></tr>");
        foreach (var e in Journal.All.Take(200))
        {
            sb.Append($"<tr><td class=num>{Fmt.Date(e.Time)}</td><td>{H(e.Title)}{(e.Undone ? " — " + H(L.T("repair.undone")) : "")}");
            if (e.Details.Count > 0)
                sb.Append("<details><summary>").Append(H(L.T("report.details"))).Append("</summary><div class=path>")
                  .Append(string.Join("<br>", e.Details.Take(300).Select(H))).Append("</div></details>");
            sb.Append($"</td><td class={Css(e.Status)}>{H(L.T("status." + e.Status))}</td></tr>");
        }
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static string Css(Severity s) => s switch { Severity.Error => "err", Severity.Warning => "warn", _ => "info" };
    private static string Css(EntryStatus s) => s switch { EntryStatus.Success => "ok", EntryStatus.Partial => "warn", EntryStatus.Failed => "err", _ => "info" };
}

/// <summary>Windows-level safety net: a System Restore point before bigger repairs.</summary>
public static class SystemRestore
{
    /// <summary>Starts PowerShell as administrator (Windows asks for permission) to create a restore point. Returns true when it ran.</summary>
    public static bool CreatePoint(string description = "IBRA SYSTEM CARE")
    {
        if (!Platform.IsWindows) return false;
        try
        {
            var cmd = $"Checkpoint-Computer -Description '{description.Replace("'", "")}' -RestorePointType 'MODIFY_SETTINGS'";
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"{cmd}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(180_000);
            var ok = p.HasExited && p.ExitCode == 0;
            Journal.Add("system", ok ? EntryStatus.Success : EntryStatus.Failed, "restore.journal", ok ? [] : [L.T("restore.failedHint")]);
            return ok;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // the person said no to the administrator prompt
        }
    }
}
