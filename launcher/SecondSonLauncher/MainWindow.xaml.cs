using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SecondSonLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => UseDarkTitleBar();
    }

    /// Windows 10 20H1+ / 11: a dark title bar to match the window.
    void UseDarkTitleBar()
    {
        const int DwmwaUseImmersiveDarkMode = 20;
        var on = 1;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
