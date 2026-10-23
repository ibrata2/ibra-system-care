namespace IbraCare.Core;

/// <summary>
/// The two languages of IBRA SYSTEM CARE. Every visible text has a key in <see cref="Strings.Table"/> with an Arabic and an
/// English version; {0}, {1}… are filled with values. Arabic is the default and is shown right to left.
/// </summary>
public static class L
{
    private static string _lang = "ar";

    public static string Language
    {
        get => _lang;
        set => _lang = value == "en" ? "en" : "ar";
    }

    public static bool IsArabic => _lang == "ar";
    public static bool IsRtl => IsArabic;

    /// <summary>List separator: Arabic comma or ", ".</summary>
    public static string Sep => IsArabic ? "، " : ", ";

    public static string T(string key, params object?[] args)
    {
        if (!Strings.Table.TryGetValue(key, out var pair))
        {
            Log.Write($"missing text: {key}");
            return key;
        }
        var text = IsArabic ? pair.Ar : pair.En;
        if (args.Length == 0) return text;
        try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args); }
        catch (FormatException) { return text; }
    }

    /// <summary>For a text that already exists in both languages (e.g. from a catalog).</summary>
    public static string Pick(string ar, string en) => IsArabic ? ar : en;
}
