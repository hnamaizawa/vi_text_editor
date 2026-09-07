using System.Text;

namespace ViTextEditor.Core.IO;

public sealed record LoadedTextFile(string Text, Encoding Encoding, string NewLine);

public static class TextFileService
{
    static TextFileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static LoadedTextFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (encoding, preambleLength) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        return new LoadedTextFile(text, encoding, DetectNewLine(text));
    }

    public static void Save(string path, string text, Encoding encoding)
    {
        File.WriteAllText(path, text, encoding);
    }

    private static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 3);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode, 2);
        }
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode, 2);
        }

        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            _ = utf8.GetString(bytes);
            return (utf8, 0);
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(932), 0);
        }
    }

    private static string DetectNewLine(string text)
    {
        if (text.Contains("\r\n", StringComparison.Ordinal)) return "\r\n";
        if (text.Contains('\n')) return "\n";
        if (text.Contains('\r')) return "\r";
        return Environment.NewLine;
    }
}
