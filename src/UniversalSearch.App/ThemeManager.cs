using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace UniversalSearch.App;

public static class ThemeManager
{
    public static bool IsDark { get; private set; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public static void Apply(string theme)
    {
        IsDark = theme == "dark";
        string[] names = ["Canvas", "Surface", "Input", "Text", "Muted", "Stroke", "Hover", "Selected", "Accent", "Warning"];
        string[] light = ["#F3F5F8", "#FCFDFE", "#EDF1F5", "#202B38", "#617084", "#DCE3EB", "#EBF0F7", "#DEEAFB", "#386BCC", "#95631A"];
        string[] dark = ["#17191F", "#22252D", "#2B303B", "#F0F3F9", "#A4AFC1", "#373E4C", "#2C3442", "#304764", "#9ABFFF", "#F0C47B"];
        for (int i = 0; i < names.Length; i++)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString((IsDark ? dark : light)[i]));
            brush.Freeze(); Application.Current.Resources["Theme" + names[i]] = brush;
        }
        foreach (Window window in Application.Current.Windows) ApplyTitleBar(window);
    }
    public static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || window.AllowsTransparency) return;
        int dark = IsDark ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref dark, 4);
    }
}
