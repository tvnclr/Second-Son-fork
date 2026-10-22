using System.IO;
namespace SecondSonLauncher.Core;

/// Every file the launcher reads or writes. The launcher lives next to shadPS4.exe; for development
/// SECONDSON_LAUNCHER_ROOT points it at another build folder.
public static class AppPaths
{
    public const string Serial = "CUSA00004";

    public static string Root { get; } = ResolveRoot();

    public static string EmulatorExe => Path.Combine(Root, "shadPS4.exe");
    public static string ReleaseFlags => Path.Combine(Root, "shadps4-flags.txt");
    public static string UserFlags => Path.Combine(Root, "shadps4-flags-user.txt");
    public static string GameTxt => Path.Combine(Root, "shadps4-game.txt");
    public static string DlssDir => Path.Combine(Root, "dlss");

    public static string UserDir => Path.Combine(Root, "user");
    public static string GlobalConfig => Path.Combine(UserDir, "config.json");
    public static string GameConfig => Path.Combine(UserDir, "custom_configs", Serial + ".json");
    public static string DefaultGameConfig =>
        Path.Combine(Root, "defaults", "user", "custom_configs", Serial + ".json");
    public static string LogDir => Path.Combine(UserDir, "log");
    public static string CacheDir => Path.Combine(UserDir, "cache");
    public static string SavesDir => Path.Combine(UserDir, "home", "1000", "savedata", Serial);
    public static string BackupDir => Path.Combine(UserDir, "save-backups");
    public static string LauncherSettings => Path.Combine(UserDir, "launcher.json");

    static string ResolveRoot()
    {
        var env = Environment.GetEnvironmentVariable("SECONDSON_LAUNCHER_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);
        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }
}
