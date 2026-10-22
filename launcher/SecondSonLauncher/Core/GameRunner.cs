using System.IO;
using System.Diagnostics;
using System.Text;

namespace SecondSonLauncher.Core;

public static class GameRunner
{
    /// Starts shadPS4.exe on the game. The emulator reads its settings files itself (release mode);
    /// SHADPS4_* variables of this process are dropped so they cannot override those files.
    public static Process Start(string gameFolder)
    {
        var eboot = Path.Combine(gameFolder, "eboot.bin");
        File.WriteAllText(AppPaths.GameTxt, eboot + "\n", new UTF8Encoding(false));

        var psi = new ProcessStartInfo(AppPaths.EmulatorExe)
        {
            WorkingDirectory = AppPaths.Root,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--game");
        psi.ArgumentList.Add(eboot);
        foreach (var key in psi.Environment.Keys.Where(k => k.StartsWith("SHADPS4_", StringComparison.OrdinalIgnoreCase)).ToList())
            psi.Environment.Remove(key);
        return Process.Start(psi) ?? throw new InvalidOperationException("shadPS4.exe did not start.");
    }

    public static bool IsRunning() =>
        Process.GetProcessesByName("shadPS4").Any(p =>
        {
            try
            {
                return string.Equals(p.MainModule?.FileName, AppPaths.EmulatorExe, StringComparison.OrdinalIgnoreCase);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        });

    public static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public static void OpenFile(string path)
    {
        if (File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public static string CreateDesktopShortcut()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var link = Path.Combine(desktop, "inFAMOUS Second Son.lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(link);
        shortcut.TargetPath = Environment.ProcessPath;
        shortcut.WorkingDirectory = AppPaths.Root;
        shortcut.IconLocation = Environment.ProcessPath + ",0";
        shortcut.Description = "inFAMOUS Second Son (shadPS4)";
        shortcut.Save();
        return link;
    }
}
