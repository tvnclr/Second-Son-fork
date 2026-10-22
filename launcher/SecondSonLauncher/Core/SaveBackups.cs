using System.IO;
using System.IO.Compression;

namespace SecondSonLauncher.Core;

/// Zip copies of user\home\1000\savedata\CUSA00004 in user\save-backups, newest first.
public static class SaveBackups
{
    public const int Keep = 10;

    public static bool HasSaves =>
        Directory.Exists(AppPaths.SavesDir) && Directory.EnumerateFileSystemEntries(AppPaths.SavesDir).Any();

    public static IReadOnlyList<string> List() =>
        Directory.Exists(AppPaths.BackupDir)
            ? Directory.GetFiles(AppPaths.BackupDir, "*.zip")
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .OrderByDescending(n => n, StringComparer.Ordinal)
                .ToList()
            : new List<string>();

    /// Returns the backup's name, or null when there is nothing to back up yet.
    public static string? Create()
    {
        if (!HasSaves)
            return null;
        Directory.CreateDirectory(AppPaths.BackupDir);
        var name = DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss");
        var zip = Path.Combine(AppPaths.BackupDir, name + ".zip");
        if (File.Exists(zip))
            return name;
        ZipFile.CreateFromDirectory(AppPaths.SavesDir, zip);
        foreach (var old in List().Skip(Keep))
            File.Delete(Path.Combine(AppPaths.BackupDir, old + ".zip"));
        return name;
    }

    /// Backs the current saves up first, then replaces them with the chosen backup.
    public static void Restore(string name)
    {
        var zip = Path.Combine(AppPaths.BackupDir, name + ".zip");
        if (!File.Exists(zip))
            throw new FileNotFoundException("Backup not found", zip);
        Create();
        var staging = AppPaths.SavesDir + ".restoring";
        if (Directory.Exists(staging))
            Directory.Delete(staging, true);
        ZipFile.ExtractToDirectory(zip, staging);
        if (Directory.Exists(AppPaths.SavesDir))
            Directory.Delete(AppPaths.SavesDir, true);
        Directory.Move(staging, AppPaths.SavesDir);
    }
}
