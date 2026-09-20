namespace ViTextEditor.Core.IO;

public static class LargeFilePolicy
{
    public const long DefaultThresholdBytes = 64L * 1024 * 1024;

    public static bool ShouldUseLargeFileMode(long fileLength, long thresholdBytes = DefaultThresholdBytes)
    {
        if (fileLength < 0) throw new ArgumentOutOfRangeException(nameof(fileLength));
        if (thresholdBytes < 1) throw new ArgumentOutOfRangeException(nameof(thresholdBytes));
        return fileLength >= thresholdBytes;
    }
}
