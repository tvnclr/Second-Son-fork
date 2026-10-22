using System.IO;
using System.Text;

namespace SecondSonLauncher.Core;

static class AtomicFile
{
    static readonly UTF8Encoding Utf8NoBom = new(false);

    /// Writes next to the target first, so a crash never leaves half a settings file behind.
    public static void WriteAllText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text, Utf8NoBom);
        File.Move(tmp, path, overwrite: true);
    }
}
