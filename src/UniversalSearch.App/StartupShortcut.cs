using System.Runtime.InteropServices;

namespace UniversalSearch.App;
public static class StartupShortcut
{
    public static void Set(bool enabled)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"UniversalSearch.lnk");
        if (!enabled) { if (File.Exists(path)) File.Delete(path); return; }
        object? shell = null, link = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            link = ((dynamic)shell!).CreateShortcut(path);
            ((dynamic)link).TargetPath = Path.Combine(AppContext.BaseDirectory,"UniversalSearch.exe");
            ((dynamic)link).Arguments = "--background";
            ((dynamic)link).WorkingDirectory = AppContext.BaseDirectory;
            ((dynamic)link).Save();
        }
        finally { foreach (var obj in new[] { link,shell }) if (obj != null && Marshal.IsComObject(obj)) Marshal.FinalReleaseComObject(obj); }
    }
}
