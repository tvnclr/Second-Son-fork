using System.IO;
using System.Buffers.Binary;
using System.Text;

namespace SecondSonLauncher.Core;

/// sce_sys\param.sfo: the game's title, serial and version (the format src/core/file_format/psf.cpp
/// reads: header, 16-byte entries, a key table and a data table).
public static class SfoReader
{
    const uint Magic = 0x46535000; // "\0PSF"

    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        byte[] b;
        try
        {
            b = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return result;
        }
        if (b.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(b) != Magic)
            return result;

        var keyTable = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8));
        var dataTable = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(12));
        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(16));
        for (var i = 0; i < count && 20 + i * 16 + 16 <= b.Length; i++)
        {
            var e = b.AsSpan(20 + i * 16);
            var keyOff = keyTable + BinaryPrimitives.ReadUInt16LittleEndian(e);
            var fmt = BinaryPrimitives.ReadUInt16LittleEndian(e[2..]);
            var len = (int)BinaryPrimitives.ReadUInt32LittleEndian(e[4..]);
            var dataOff = dataTable + (int)BinaryPrimitives.ReadUInt32LittleEndian(e[12..]);
            if (keyOff >= b.Length || dataOff + len > b.Length)
                continue;
            var keyEnd = Array.IndexOf(b, (byte)0, keyOff);
            var key = Encoding.ASCII.GetString(b, keyOff, (keyEnd < 0 ? b.Length : keyEnd) - keyOff);
            result[key] = fmt == 0x0404
                ? BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(dataOff)).ToString()
                : Encoding.UTF8.GetString(b, dataOff, len).TrimEnd('\0');
        }
        return result;
    }
}
