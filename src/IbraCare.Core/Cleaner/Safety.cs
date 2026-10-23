using System.Text.RegularExpressions;

namespace IbraCare.Core.Cleaner;

/// <summary>The rules that keep the cleaner from ever touching the wrong place.</summary>
public static class Safety
{
    /// <summary>Folders that must never be a cleaning root themselves (only known subfolders inside some of them are).
    /// If %TEMP% were pointed at C:\ or the user folder, its target is refused instead of emptying that folder.</summary>
    public static IEnumerable<string> ForbiddenRoots()
    {
        string?[] vars = ["%WINDIR%", "%WINDIR%\\System32", "%WINDIR%\\SysWOW64", "%WINDIR%\\WinSxS", "%PROGRAMFILES%", "%PROGRAMFILES(X86)%",
            "%PROGRAMDATA%", "%USERPROFILE%", "%LOCALAPPDATA%", "%APPDATA%", "%PUBLIC%", "%SYSTEMDRIVE%\\Users"];
        foreach (var v in vars)
        {
            var p = Env.Expand(v!);
            if (p is not null) yield return Normalize(p);
        }
        foreach (var f in new[] { Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.Desktop, Environment.SpecialFolder.MyPictures,
                     Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.UserProfile })
        {
            var p = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(p)) yield return Normalize(p);
        }
    }

    /// <summary>Folders whose contents are never scanned, wherever they appear (our own backups and quarantine).</summary>
    public static IEnumerable<string> ProtectedTrees()
    {
        yield return Normalize(Paths.AppData);
    }

    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        return full.Length > root.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
    }

    /// <summary>A root is acceptable when it is not a drive root, has at least two levels, and is not one of the forbidden folders.</summary>
    public static bool IsAcceptableRoot(string root, out string reason)
    {
        reason = "";
        var n = Normalize(root);
        var driveRoot = Path.GetPathRoot(n) ?? "";
        if (n.Length <= driveRoot.Length)
        {
            reason = L.T("safety.driveRoot");
            return false;
        }
        var depth = n[driveRoot.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
        if (depth < 2)
        {
            reason = L.T("safety.tooShallow");
            return false;
        }
        if (ForbiddenRoots().Any(f => string.Equals(f, n, Platform.PathComparison)))
        {
            reason = L.T("safety.forbidden");
            return false;
        }
        if (ProtectedTrees().Any(p => IsInside(n, p) || IsInside(p, n)))
        {
            reason = L.T("safety.ownData");
            return false;
        }
        return true;
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> or somewhere below it.</summary>
    public static bool IsInside(string path, string folder)
    {
        var p = Normalize(path);
        var f = Normalize(folder);
        if (string.Equals(p, f, Platform.PathComparison)) return true;
        var withSep = f.EndsWith(Path.DirectorySeparatorChar) ? f : f + Path.DirectorySeparatorChar;
        return p.StartsWith(withSep, Platform.PathComparison);
    }

    public static bool IsLink(FileSystemInfo info)
    {
        try { return info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.LinkTarget is not null; }
        catch (Exception) { return true; }
    }

    // ---------- "what I want to keep" ----------

    /// <summary>A keep rule is either a path (the file, or everything inside the folder) or a name pattern like *.log or setup*.exe.</summary>
    public sealed class KeepRules
    {
        private readonly List<string> _paths = [];
        private readonly List<Regex> _names = [];
        private readonly List<Regex> _fullPatterns = [];

        public KeepRules(IEnumerable<string> rules)
        {
            foreach (var raw in rules)
            {
                var r = raw.Trim().Trim('"');
                if (r.Length == 0 || r.StartsWith('#')) continue;
                var expanded = Env.Expand(r) ?? r;
                if (Path.DirectorySeparatorChar == '/') expanded = expanded.Replace('\\', '/');
                var hasWild = expanded.IndexOfAny(['*', '?']) >= 0;
                var hasSep = expanded.Contains('/') || expanded.Contains('\\');
                if (!hasWild && hasSep) _paths.Add(Normalize(expanded));
                else if (!hasSep) _names.Add(Wildcard(expanded));
                else _fullPatterns.Add(Wildcard(expanded));
            }
        }

        public bool Matches(string fullPath)
        {
            foreach (var p in _paths) if (IsInside(fullPath, p)) return true;
            var name = Path.GetFileName(fullPath);
            foreach (var n in _names) if (n.IsMatch(name)) return true;
            foreach (var f in _fullPatterns) if (f.IsMatch(fullPath)) return true;
            return false;
        }

        public bool IsEmpty => _paths.Count + _names.Count + _fullPatterns.Count == 0;
    }

    public static Regex Wildcard(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.CultureInvariant | (Platform.IsWindows ? RegexOptions.IgnoreCase : RegexOptions.None));
}
