using ViTextEditor.Core.Editor;
using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class V013WorkspaceFeatureTests
{
    [Fact]
    public void LargeFilePolicyUses64MiBThresholdByDefault()
    {
        Assert.False(LargeFilePolicy.ShouldUseLargeFileMode(64L * 1024 * 1024 - 1));
        Assert.True(LargeFilePolicy.ShouldUseLargeFileMode(64L * 1024 * 1024));
    }

    [Fact]
    public void NormalTextLoaderRefusesLargeFileBeforeReadAllBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vi_text_editor_large_{Guid.NewGuid():N}.txt");
        try
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.SetLength(LargeFilePolicy.DefaultThresholdBytes);

            var ex = Assert.Throws<IOException>(() => TextFileService.Load(path));
            Assert.Contains("Large File", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void RecentFileListMovesDuplicatesToFrontAndCapsCapacity()
    {
        var recent = new RecentFileList(3);
        recent.Add("a.txt");
        recent.Add("b.txt");
        recent.Add("c.txt");
        recent.Add("a.txt");
        recent.Add("d.txt");

        Assert.Equal(3, recent.Items.Count);
        Assert.EndsWith("d.txt", recent.Items[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("a.txt", recent.Items[1], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("c.txt", recent.Items[2], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JsonFormatterIndentsAndPreservesRequestedNewLine()
    {
        Assert.True(JsonFormattingService.TryFormat("{\"a\":1,\"b\":[true,false]}", "\r\n", out var formatted, out var error));
        Assert.Null(error);
        Assert.Contains("\r\n", formatted, StringComparison.Ordinal);
        Assert.Contains("  \"a\": 1", formatted, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonFormatterReportsInvalidJson()
    {
        Assert.False(JsonFormattingService.TryFormat("{ bad", "\n", out _, out var error));
        Assert.NotNull(error);
    }
}
