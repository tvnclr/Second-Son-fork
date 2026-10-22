using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SecondSonLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => UseDarkTitleBar();
        UseGameIcon();
    }

    /// The title bar and taskbar icon: app.ico when publish.ps1 built it in (game art, not in the repo).
    void UseGameIcon()
    {
        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/app.ico"));
        }
        catch (IOException)
        {
            // Built without the icon: Windows' default.
        }
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
