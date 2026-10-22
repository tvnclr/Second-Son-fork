using System.IO;
using System.Text;

namespace SecondSonLauncher.Core;

/// shadps4-flags-user.txt: NAME=VALUE lines the emulator turns into environment variables before it
/// starts (src/common/release_mode.cpp). The first line for a name wins, and the file wins over the
/// release's shadps4-flags.txt. The launcher owns one block at the top of the file; every other line
/// is the player's and is kept as it is.
public sealed class FlagsFile
{
    const string BlockBegin = "# >>> SecondSonLauncher - written by the launcher, edit it there >>>";
    const string BlockEnd = "# <<< SecondSonLauncher <<<";

    readonly Dictionary<string, string> _release;
    readonly Dictionary<string, string> _manual;
    readonly Dictionary<string, string> _managed;
    readonly List<string> _otherLines;

    FlagsFile(Dictionary<string, string> release, Dictionary<string, string> manual,
              Dictionary<string, string> managed, List<string> otherLines)
    {
        _release = release;
        _manual = manual;
        _managed = managed;
        _otherLines = otherLines;
    }

    public static FlagsFile Load()
    {
        var release = NewMap();
        if (File.Exists(AppPaths.ReleaseFlags))
            foreach (var line in File.ReadAllLines(AppPaths.ReleaseFlags))
                AddFirst(release, line);

        var manual = NewMap();
        var managed = NewMap();
        var other = new List<string>();
        if (File.Exists(AppPaths.UserFlags))
        {
            var inBlock = false;
            foreach (var line in File.ReadAllLines(AppPaths.UserFlags))
            {
                if (line.Trim() == BlockBegin) { inBlock = true; continue; }
                if (line.Trim() == BlockEnd) { inBlock = false; continue; }
                if (inBlock)
                {
                    AddFirst(managed, line);
                    continue;
                }
                other.Add(line);
                AddFirst(manual, line);
            }
        }
        while (other.Count > 0 && other[0].Trim().Length == 0)
            other.RemoveAt(0);
        return new FlagsFile(release, manual, managed, other);
    }

    /// The value the player chose (launcher block first, then their own lines), or null.
    public string? GetUser(string name) =>
        _managed.TryGetValue(name, out var v) ? v : _manual.TryGetValue(name, out var m) ? m : null;

    /// The value the emulator will use: the player's, else the release file's, else null.
    public string? GetEffective(string name) =>
        GetUser(name) ?? (_release.TryGetValue(name, out var r) ? r : null);

    /// null removes the line. A value equal to the release default is not written unless one of the
    /// player's own lines would otherwise win.
    public void Set(string name, string? value)
    {
        var isReleaseDefault = value != null && _release.TryGetValue(name, out var r) && r == value;
        if (value == null || (isReleaseDefault && !_manual.ContainsKey(name)))
            _managed.Remove(name);
        else
            _managed[name] = value;
    }

    public void Save()
    {
        var sb = new StringBuilder();
        if (_managed.Count > 0)
        {
            sb.AppendLine(BlockBegin);
            foreach (var (name, value) in _managed.OrderBy(p => p.Key, StringComparer.Ordinal))
                sb.Append(name).Append('=').AppendLine(value);
            sb.AppendLine(BlockEnd);
            if (_otherLines.Count > 0)
                sb.AppendLine();
        }
        foreach (var line in _otherLines)
            sb.AppendLine(line);
        AtomicFile.WriteAllText(AppPaths.UserFlags, sb.ToString());
    }

    static Dictionary<string, string> NewMap() => new(StringComparer.OrdinalIgnoreCase);

    static void AddFirst(Dictionary<string, string> map, string line)
    {
        var t = line.Trim();
        if (t.Length == 0 || t[0] == '#')
            return;
        var eq = t.IndexOf('=');
        if (eq <= 0)
            return;
        map.TryAdd(t[..eq].Trim(), t[(eq + 1)..].Trim());
    }
}
