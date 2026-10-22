using System.IO;
using System.Text.Json;

namespace SecondSonLauncher.Core;

/// The launcher's own choices (user\launcher.json); the game's settings live in the emulator's files.
public sealed class LauncherSettings
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string? GameFolder { get; set; }
    public bool StartDirectly { get; set; }
    public bool CloseOnPlay { get; set; }
    public bool BackupBeforePlay { get; set; } = true;

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.LauncherSettings))
                return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(AppPaths.LauncherSettings), Options)
                       ?? new LauncherSettings();
        }
        catch (JsonException)
        {
        }
        return new LauncherSettings();
    }

    public void Save() => AtomicFile.WriteAllText(AppPaths.LauncherSettings, JsonSerializer.Serialize(this, Options));

    /// The folder the emulator remembers (shadps4-game.txt holds the path of eboot.bin).
    public static string? FolderFromGameTxt()
    {
        try
        {
            if (!File.Exists(AppPaths.GameTxt))
                return null;
            var eboot = File.ReadAllText(AppPaths.GameTxt).Trim();
            return eboot.Length > 0 ? Path.GetDirectoryName(eboot) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
