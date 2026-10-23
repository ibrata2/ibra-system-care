namespace IbraCare.Core.Repair;

public enum Hive { CurrentUser, LocalMachine }

/// <summary>One registry value, kept as text so it can be stored in a backup and written back exactly.</summary>
public sealed record RegValue(string Name, string Kind, string Data);

/// <summary>The few registry operations the app needs. Windows uses the real registry; tests use <see cref="MemoryRegistry"/>.</summary>
public interface IRegistryAccess
{
    RegValue? Get(Hive hive, string key, string name);
    IReadOnlyList<RegValue> List(Hive hive, string key);
    void Set(Hive hive, string key, RegValue value);
    void Delete(Hive hive, string key, string name);
    IReadOnlyList<string> SubKeys(Hive hive, string key);
}

public static class Registries
{
    /// <summary>The real registry on Windows, an empty in-memory one elsewhere.</summary>
    public static IRegistryAccess Default { get; set; } = OperatingSystem.IsWindows() ? new WindowsRegistry() : new MemoryRegistry();
}

public sealed class MemoryRegistry : IRegistryAccess
{
    private readonly Dictionary<string, Dictionary<string, RegValue>> _keys = new(StringComparer.OrdinalIgnoreCase);

    private static string K(Hive h, string key) => h + "\\" + key.Trim('\\');

    public RegValue? Get(Hive hive, string key, string name) =>
        _keys.TryGetValue(K(hive, key), out var k) && k.TryGetValue(name, out var v) ? v : null;

    public IReadOnlyList<RegValue> List(Hive hive, string key) =>
        _keys.TryGetValue(K(hive, key), out var k) ? k.Values.ToList() : [];

    public void Set(Hive hive, string key, RegValue value)
    {
        if (!_keys.TryGetValue(K(hive, key), out var k)) _keys[K(hive, key)] = k = new(StringComparer.OrdinalIgnoreCase);
        k[value.Name] = value;
    }

    public void Delete(Hive hive, string key, string name)
    {
        if (_keys.TryGetValue(K(hive, key), out var k)) k.Remove(name);
    }

    public IReadOnlyList<string> SubKeys(Hive hive, string key)
    {
        var prefix = K(hive, key) + "\\";
        return _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..].Split('\\')[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class WindowsRegistry : IRegistryAccess
{
    private static Microsoft.Win32.RegistryKey Root(Hive h)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        return h == Hive.LocalMachine
            ? Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
            : Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Default);
    }

    public RegValue? Get(Hive hive, string key, string name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var root = Root(hive);
        using var k = root.OpenSubKey(key);
        if (k is null) return null;
        var raw = k.GetValue(name, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
        return raw is null ? null : ToValue(name, k.GetValueKind(name), raw);
    }

    public IReadOnlyList<RegValue> List(Hive hive, string key)
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var root = Root(hive);
        using var k = root.OpenSubKey(key);
        if (k is null) return [];
        var list = new List<RegValue>();
        foreach (var n in k.GetValueNames())
        {
            try
            {
                var raw = k.GetValue(n, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (raw is not null) list.Add(ToValue(n, k.GetValueKind(n), raw));
            }
            catch (Exception) { }
        }
        return list;
    }

    public void Set(Hive hive, string key, RegValue value)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var root = Root(hive);
        using var k = root.CreateSubKey(key, true);
        var kind = Enum.TryParse<Microsoft.Win32.RegistryValueKind>(value.Kind, out var vk) ? vk : Microsoft.Win32.RegistryValueKind.String;
        object data = kind switch
        {
            Microsoft.Win32.RegistryValueKind.DWord => unchecked((int)uint.Parse(value.Data)),
            Microsoft.Win32.RegistryValueKind.QWord => unchecked((long)ulong.Parse(value.Data)),
            Microsoft.Win32.RegistryValueKind.Binary => Convert.FromBase64String(value.Data),
            Microsoft.Win32.RegistryValueKind.MultiString => value.Data.Split('\n'),
            _ => value.Data,
        };
        k.SetValue(value.Name, data, kind);
    }

    public void Delete(Hive hive, string key, string name)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var root = Root(hive);
        using var k = root.OpenSubKey(key, true);
        k?.DeleteValue(name, false);
    }

    public IReadOnlyList<string> SubKeys(Hive hive, string key)
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var root = Root(hive);
        using var k = root.OpenSubKey(key);
        return k?.GetSubKeyNames() ?? [];
    }

    private static RegValue ToValue(string name, Microsoft.Win32.RegistryValueKind kind, object raw) => new(name, kind.ToString(), raw switch
    {
        byte[] b => Convert.ToBase64String(b),
        string[] m => string.Join('\n', m),
        int i => unchecked((uint)i).ToString(),
        long l => unchecked((ulong)l).ToString(),
        _ => raw.ToString() ?? "",
    });
}
