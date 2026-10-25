using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class CsvTableParserTests
{
    [Fact]
    public void Parse_HandlesQuotedCommaEscapedQuoteAndMultilineField()
    {
        var table = CsvTableParser.Parse("name,note\r\n\"A,B\",\"line1\r\nline2\"\r\n\"say \"\"hi\"\"\",ok");

        Assert.Equal(',', table.Delimiter);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(["name", "note"], table.Rows[0]);
        Assert.Equal(["A,B", "line1\r\nline2"], table.Rows[1]);
        Assert.Equal(["say \"hi\"", "ok"], table.Rows[2]);
        Assert.False(table.Truncated);
    }

    [Theory]
    [InlineData("a\tb\tc\n1\t2\t3", '\t')]
    [InlineData("a;b;c\n1;2;3", ';')]
    [InlineData("a,b,c\n1,2,3", ',')]
    [InlineData("\"a,b\",c\n1,2", ',')]
    public void Parse_DetectsDelimiterOutsideQuotes(string text, char expected)
    {
        Assert.Equal(expected, CsvTableParser.Parse(text).Delimiter);
    }

    [Fact]
    public void Parse_RemovesUtf8BomAndDoesNotAddTrailingBlankRow()
    {
        var table = CsvTableParser.Parse("\uFEFFname,value\r\na,1\r\n");

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("name", table.Rows[0][0]);
    }

    [Fact]
    public void Parse_ReportsRowAndColumnTruncation()
    {
        var table = CsvTableParser.Parse("a,b,c\n1,2,3\n4,5,6", maxRows: 2, maxColumns: 2);

        Assert.True(table.Truncated);
        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, row => Assert.Equal(2, row.Count));
    }
}
