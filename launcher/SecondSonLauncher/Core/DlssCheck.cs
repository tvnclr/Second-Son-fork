using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SecondSonLauncher.Core;

/// The five Streamline files the README asks for (dlss\, from Streamline SDK 2.14.16 bin\x64). The
/// emulator itself checks NVIDIA's signature (src/video_core/renderer_vulkan/vk_dlss.cpp); this only
/// tells the player early.
public static class DlssCheck
{
    public const string SdkVersion = "2.14.16";
    public const string DownloadPage = "https://github.com/NVIDIA-RTX/Streamline/releases/tag/v2.14.16";

    public static readonly string[] Files =
        { "sl.interposer.dll", "sl.common.dll", "sl.dlss.dll", "sl.pcl.dll", "nvngx_dlss.dll" };

    public static (bool Ready, string Text) Check()
    {
        var missing = Files.Where(f => !File.Exists(Path.Combine(AppPaths.DlssDir, f))).ToList();
        if (missing.Count == Files.Length)
            return (false, $"Not installed: copy the five files from Streamline SDK {SdkVersion} (bin\\x64) into the dlss folder.");
        if (missing.Count > 0)
            return (false, "Missing: " + string.Join(", ", missing));
        var unsigned = Files.Where(f => !SignedByNvidia(Path.Combine(AppPaths.DlssDir, f))).ToList();
        if (unsigned.Count > 0)
            return (false, "Not signed by NVIDIA: " + string.Join(", ", unsigned) + ". Use the files from NVIDIA's own download.");
        return (true, "Ready: the five Streamline files are in place and signed by NVIDIA.");
    }

    static bool SignedByNvidia(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057
            var cert = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            return cert.Subject.Contains("O=NVIDIA Corporation", StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
