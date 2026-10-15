using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace UniversalSearch.App;

public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    readonly HwndSource source;
    readonly Action show;
    public string Current { get; private set; } = "";
    public HotkeyService(Window window, Action show)
    {
        this.show = show;
        // The search window's HWND can change when WPF changes taskbar visibility.
        // Keep registration on an independent message-only window for the entire tray lifetime.
        source = new HwndSource(new HwndSourceParameters("UniversalSearch.Hotkey") { ParentWindow = new IntPtr(-3), WindowStyle = 0, Width = 0, Height = 0 });
        source.AddHook(Hook);
    }
    public static bool Parse(string text, out uint modifiers, out uint key)
    {
        modifiers = 0x4000; key = 0; var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        foreach (var part in parts[..^1])
        {
            var value = part.ToLowerInvariant() switch { "alt" => 1u, "ctrl" => 2u, "shift" => 4u, "win" => 8u, _ => 0u };
            if (value == 0) return false; modifiers |= value;
        }
        if (!Enum.TryParse<Key>(parts[^1], true, out var parsed) || parsed == Key.None) return false;
        key = (uint)KeyInterop.VirtualKeyFromKey(parsed); return key != 0;
    }
    public bool Register(string text)
    {
        if (!Parse(text, out var modifiers, out var key)) return false;
        var previous = Current; UnregisterHotKey(source.Handle, 1);
        if (RegisterHotKey(source.Handle, 1, modifiers, key)) { Current = text; return true; }
        if (Parse(previous, out modifiers, out key)) RegisterHotKey(source.Handle, 1, modifiers, key);
        return false;
    }
    IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    { if (msg == 0x0312 && wParam.ToInt32() == 1) { show(); handled = true; } return IntPtr.Zero; }
    public void Dispose() { UnregisterHotKey(source.Handle, 1); source.RemoveHook(Hook); source.Dispose(); }
}
public static class ShellIcons
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct SHFILEINFO
    {
        public IntPtr Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attributes, out SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attributes, out SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint attributes, out uint resultAttributes);
    [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] static extern void CoUninitialize();
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static readonly Dictionary<string, ImageSource?> cache = new();
    public static ImageSource? Get(UniversalSearch.Core.SearchEntry e)
    {
        if (e.Kind > UniversalSearch.Core.EntryKind.Application) return null;
        bool folder = e.Kind == UniversalSearch.Core.EntryKind.Folder;
        var key = e.Kind == UniversalSearch.Core.EntryKind.Application ? e.Path : folder ? "folder" : e.Extension;
        lock (cache)
        {
            if (cache.TryGetValue(key, out var cached)) return cached;
            uint flags = 0x100; // icon, large (32px)
            if (e.Kind != UniversalSearch.Core.EntryKind.Application) flags |= 0x10; // use file attributes
            SHFILEINFO info = default;
            var com = CoInitializeEx(IntPtr.Zero, 0);
            try
            {
                if (e.Path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                {
                    // AppsFolder entries are shell objects, not filesystem paths.
                    // Resolve their PIDLs on the search worker, then ask Windows for the registered icon.
                    IntPtr pidl = IntPtr.Zero;
                    try
                    {
                        if (SHParseDisplayName(e.Path, IntPtr.Zero, out pidl, 0, out _) >= 0 && pidl != IntPtr.Zero)
                            SHGetFileInfo(pidl, 0, out info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags | 0x8);
                    }
                    finally { if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
                }
                else SHGetFileInfo(e.Path, folder ? 0x10u : 0x80u, out info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
            }
            finally { if (com >= 0) CoUninitialize(); }
            ImageSource? image = null;
            if (info.Icon != IntPtr.Zero) try { image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); } finally { DestroyIcon(info.Icon); }
            if (cache.Count > 256) cache.Clear(); cache[key] = image; return image;
        }
    }
}
