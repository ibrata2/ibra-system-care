using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace IbraCare.Core;

/// <summary>Where IBRA SYSTEM CARE keeps its own files (settings, history, backups, quarantine).</summary>
public static class Paths
{
    /// <summary>Tests and snapshots point this somewhere else so they never touch the real data.</summary>
    public static string? AppDataOverride { get; set; }

    public const string AppFolderName = "IBRA System Care";

    public static string AppData
    {
        get
        {
            var dir = AppDataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string Backups => Ensure(Path.Combine(AppData, "Backups"));
    public static string Downloads => Ensure(Path.Combine(AppData, "Downloads"));

    /// <summary>Reports are written where the person can find them: Documents\IBRA System Care\Reports.</summary>
    public static string Reports
    {
        get
        {
            if (AppDataOverride is not null) return Ensure(Path.Combine(AppDataOverride, "Reports"));
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(docs)) docs = AppData;
            return Ensure(Path.Combine(docs, AppFolderName, "Reports"));
        }
    }

    public static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}

/// <summary>Expands %VARIABLES% in paths. Tests replace the values to point at a fake Windows tree.</summary>
public static class Env
{
    public static Dictionary<string, string>? Override { get; set; }

    public static string? Get(string name)
    {
        if (Override is not null) return Override.TryGetValue(name, out var v) ? v : null;
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value) && OperatingSystem.IsWindows())
        {
            // A few names Windows always has, even when the variable is missing from this process.
            value = name.ToUpperInvariant() switch
            {
                "LOCALAPPDATA" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "APPDATA" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PROGRAMDATA" => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "WINDIR" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "USERPROFILE" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "TEMP" or "TMP" => Path.GetTempPath().TrimEnd('\\'),
                "PROGRAMFILES" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PROGRAMFILES(X86)" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                _ => null,
            };
        }
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Returns null when a variable in the text is unknown (so a target is skipped instead of guessed).</summary>
    public static string? Expand(string text)
    {
        var result = new System.Text.StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf('%', i);
            if (start < 0) { result.Append(text, i, text.Length - i); break; }
            var end = text.IndexOf('%', start + 1);
            if (end < 0) { result.Append(text, i, text.Length - i); break; }
            result.Append(text, i, start - i);
            var value = Get(text[(start + 1)..end]);
            if (value is null) return null;
            result.Append(value.TrimEnd('\\', '/'));
            i = end + 1;
        }
        var s = result.ToString();
        return Path.DirectorySeparatorChar == '/' ? s.Replace('\\', '/') : s;
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Writes to a temporary file first so a crash never leaves a half-written file behind.</summary>
    public static void SaveAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, true);
    }

    public static T? Load<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception e)
        {
            Log.Write($"json {path}: {e.Message}");
            return null;
        }
    }
}

/// <summary>A small text log next to the settings, for diagnosing problems on someone else's computer.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                var file = Path.Combine(Paths.AppData, "log.txt");
                if (File.Exists(file) && new FileInfo(file).Length > 2_000_000) File.Move(file, file + ".old", true);
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
            }
        }
        catch (Exception) { }
    }
}

public static class Fmt
{
    /// <summary>"1.5 GB" — always with Latin digits and a dot, shown left to right by the UI.</summary>
    public static string Size(long bytes)
    {
        if (bytes < 0) return "—";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes;
        var u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : v.ToString(v >= 100 ? "0" : v >= 10 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + " " + units[u];
    }

    public static string Count(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    public static string Date(DateTime t) => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}

public static class Hash
{
    public static string Sha256(string file)
    {
        using var s = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(s));
    }

    public static string Sha256(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
}

public static class Platform
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    public static bool IsAdmin
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return Environment.UserName == "root";
            try
            {
                using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception) { return false; }
        }
    }

    public static bool IsProcessRunning(params string[] names)
    {
        try
        {
            return System.Diagnostics.Process.GetProcesses().Any(p =>
            {
                try { return names.Any(n => string.Equals(p.ProcessName, n, StringComparison.OrdinalIgnoreCase)); }
                catch (Exception) { return false; }
                finally { p.Dispose(); }
            });
        }
        catch (Exception) { return false; }
    }

    public static StringComparison PathComparison => IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

/// <summary>Turns exceptions into short, understandable sentences in the chosen language.</summary>
public static class Errors
{
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    public static string Friendly(Exception e) => e switch
    {
        UnauthorizedAccessException => L.T("err.access"),
        IOException io when io.HResult is SharingViolation or LockViolation => L.T("err.inuse"),
        PathTooLongException => L.T("err.pathlong"),
        DirectoryNotFoundException or FileNotFoundException => L.T("err.notfound"),
        HttpRequestException => L.T("err.network"),
        TaskCanceledException or OperationCanceledException => L.T("err.cancelled"),
        IOException io when io.Message.Contains("space", StringComparison.OrdinalIgnoreCase) => L.T("err.diskfull"),
        _ => L.T("err.generic", e.Message),
    };

    public static bool IsInUse(Exception e) => e is IOException { HResult: SharingViolation or LockViolation };
}

public static class Http
{
    public static readonly HttpClient Client = Make();

    private static HttpClient Make()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("IBRA-System-Care/1.0");
        return c;
    }
}

