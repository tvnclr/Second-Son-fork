using System.IO;
namespace SecondSonLauncher.Core;

public enum IssueLevel { Ok, Warning, Error }

public sealed record Issue(IssueLevel Level, string Text);

public sealed record GameReport(
    bool Found,
    string Folder,
    string Title,
    string TitleId,
    string Version,
    string? UpdateFolder,
    string? Background,
    IReadOnlyList<Issue> Issues)
{
    public bool HasErrors => Issues.Any(i => i.Level == IssueLevel.Error);
    public bool HasWarnings => Issues.Any(i => i.Level == IssueLevel.Warning);
    public bool Ready => Found && !HasErrors;
}

/// Checks a game folder the way this build needs it: eboot.bin, the right game, update 1.07 in a
/// -UPDATE / -patch folder next to it (src/core/file_sys/fs.cpp), and files that are not empty.
public static class GameInspector
{
    public static readonly string[] SupportedSerials = { "CUSA00004", "CUSA00223" };

    /// PKG Extractor 1.0 wrote zeros instead of data for parts of packages over 2 GB (fixed in 1.1);
    /// the game then crashed right after loading its language files. These files show it fastest.
    static readonly string[] SampleFiles =
    {
        @"art\cache\all_shaders.xpps",
        @"art\cache\lang_english_text.xpps",
        @"art\cache\lang_portuguese_text.xpps",
        @"art\cache\a1.xpps",
    };

    public static GameReport Inspect(string? folder)
    {
        var issues = new List<Issue>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            issues.Add(new(IssueLevel.Error, "Choose the game folder: the one that holds eboot.bin and sce_sys."));
            return new GameReport(false, folder ?? "", "inFAMOUS Second Son", "", "", null, null, issues);
        }
        if (!File.Exists(Path.Combine(folder, "eboot.bin")))
        {
            issues.Add(new(IssueLevel.Error, "This folder has no eboot.bin. Choose the game's own folder."));
            return new GameReport(false, folder, "inFAMOUS Second Son", "", "", null, null, issues);
        }

        var sfo = SfoReader.Read(Path.Combine(folder, "sce_sys", "param.sfo"));
        var title = sfo.GetValueOrDefault("TITLE", "inFAMOUS Second Son").Replace("™", "");
        var titleId = sfo.GetValueOrDefault("TITLE_ID", "");
        var contentId = sfo.GetValueOrDefault("CONTENT_ID", "");
        var version = sfo.GetValueOrDefault("APP_VER") ?? sfo.GetValueOrDefault("VERSION", "");

        if (sfo.GetValueOrDefault("CATEGORY") == "ac" || contentId.Contains("UNLOCK", StringComparison.Ordinal))
            issues.Add(new(IssueLevel.Error,
                "The game's sce_sys folder was overwritten by a DLC package. Extract the base game again " +
                "into an empty folder (DLCs go to their own folder)."));
        else if (titleId.Length > 0 && !SupportedSerials.Contains(titleId))
            issues.Add(new(IssueLevel.Warning,
                $"{titleId} is not inFAMOUS Second Son (CUSA00004 / CUSA00223); this build is made for that game only."));

        var update = FindUpdateFolder(folder);
        if (update == null)
        {
            issues.Add(new(IssueLevel.Warning,
                $"Update 1.07 not found. Put it next to the game as \"{Path.GetFileName(folder)}-patch\" " +
                "or \"-UPDATE\"; this build is tested with 1.07."));
        }
        else
        {
            var usfo = SfoReader.Read(Path.Combine(update, "sce_sys", "param.sfo"));
            version = usfo.GetValueOrDefault("APP_VER") ?? version;
            if (version != "01.07")
                issues.Add(new(IssueLevel.Warning, $"The update is version {version}; this build is tested with 01.07."));
        }

        var empty = SampleFiles
            .SelectMany(f => new[] { Path.Combine(folder, f), update == null ? null : Path.Combine(update, f) })
            .Where(p => p != null && File.Exists(p) && IsZeroFilled(p!, sample: true))
            .Select(p => Path.GetFileName(p!))
            .Distinct()
            .ToList();
        if (empty.Count > 0)
            issues.Add(new(IssueLevel.Error,
                $"Some game files are empty ({string.Join(", ", empty)}). PKG Extractor 1.0 does this with " +
                "packages over 2 GB: extract the PKGs again with version 1.1 or newer."));

        if (issues.Count == 0)
            issues.Add(new(IssueLevel.Ok, "Game files look good."));

        var background = new[] { update, folder }
            .Where(d => d != null)
            .SelectMany(d => new[] { Path.Combine(d!, "sce_sys", "pic1.png"), Path.Combine(d!, "sce_sys", "pic0.png") })
            .FirstOrDefault(File.Exists);

        return new GameReport(true, folder, title, titleId, version, update, background, issues);
    }

    public static string? FindUpdateFolder(string folder)
    {
        var trimmed = folder.TrimEnd(Path.DirectorySeparatorChar);
        foreach (var suffix in new[] { "-UPDATE", "-patch" })
        {
            var candidate = trimmed + suffix;
            if (File.Exists(Path.Combine(candidate, "eboot.bin")) || Directory.Exists(Path.Combine(candidate, "sce_sys")))
                return candidate;
        }
        return null;
    }

    /// Reads every file of the game and its update and lists the ones that hold only zeros.
    public static Task<List<string>> FindEmptyFilesAsync(string folder, IProgress<string> progress,
                                                         CancellationToken cancel) => Task.Run(() =>
    {
        var roots = new[] { folder, FindUpdateFolder(folder) }.Where(r => r != null).Select(r => r!).ToList();
        var files = roots.SelectMany(r => Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories)).ToList();
        var empty = new List<string>();
        for (var i = 0; i < files.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var f = files[i];
            // psreserved.dat is all zeros in every game.
            if (!f.EndsWith("psreserved.dat", StringComparison.OrdinalIgnoreCase) && IsZeroFilled(f, sample: false))
                empty.Add(Path.GetRelativePath(Path.GetDirectoryName(folder)!, f));
            if (i % 25 == 0)
                progress.Report($"Checking {i + 1} of {files.Count} files...");
        }
        return empty;
    }, cancel);

    /// sample = only the first and last 256 KB (fast); otherwise the whole file.
    static bool IsZeroFilled(string path, bool sample)
    {
        const int block = 256 * 1024;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, block);
            if (fs.Length == 0)
                return false;
            var buf = new byte[block];
            if (sample)
            {
                if (!ZeroBlock(fs, buf))
                    return false;
                if (fs.Length > block)
                {
                    fs.Seek(Math.Max(block, fs.Length - block), SeekOrigin.Begin);
                    return ZeroBlock(fs, buf);
                }
                return true;
            }
            int n;
            while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                if (buf.AsSpan(0, n).IndexOfAnyExcept((byte)0) >= 0)
                    return false;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    static bool ZeroBlock(FileStream fs, byte[] buf)
    {
        var n = fs.Read(buf, 0, buf.Length);
        return buf.AsSpan(0, n).IndexOfAnyExcept((byte)0) < 0;
    }
}
