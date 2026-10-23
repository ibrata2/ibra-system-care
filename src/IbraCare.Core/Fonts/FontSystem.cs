using System.Runtime.InteropServices;

namespace IbraCare.Core.Fonts;

/// <summary>Tells Windows about fonts added or removed now, so open programs see the change without signing out.</summary>
public static partial class FontSystem
{
    private const int WM_FONTCHANGE = 0x001D;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [LibraryImport("gdi32.dll", EntryPoint = "AddFontResourceW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int AddFontResource(string file);

    [LibraryImport("gdi32.dll", EntryPoint = "RemoveFontResourceW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveFontResource(string file);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    public static void Load(string file)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { AddFontResource(file); } catch (Exception e) { Log.Write($"AddFontResource: {e.Message}"); }
    }

    public static void Unload(string file)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            // A font can be loaded more than once; remove until Windows says it is gone.
            for (var i = 0; i < 8 && RemoveFontResource(file); i++) { }
        }
        catch (Exception e) { Log.Write($"RemoveFontResource: {e.Message}"); }
    }

    public static void Broadcast()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { SendMessageTimeout(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 1000, out _); }
        catch (Exception e) { Log.Write($"WM_FONTCHANGE: {e.Message}"); }
    }
}
