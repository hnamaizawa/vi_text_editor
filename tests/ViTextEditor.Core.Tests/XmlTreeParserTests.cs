using ViTextEditor.Core.IO;
using Xunit;

namespace ViTextEditor.Core.Tests;

public sealed class XmlTreeParserTests
{
    [Fact]
    public void ParsesElementsAttributesTextCDataAndComments()
    {
        var result = XmlTreeParser.Parse(
            "<?xml version=\"1.0\"?><root id=\"7\"><item>日本語</item><!-- note --><![CDATA[a < b]]></root>");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.ElementCount);
        var documentChildren = result.Root!.Children;
        Assert.Equal(XmlTreeNodeKind.Declaration, documentChildren[0].Kind);
        var root = Assert.Single(documentChildren.Where(node => node.Kind == XmlTreeNodeKind.Element));
        Assert.Contains(root.Children, node => node.Kind == XmlTreeNodeKind.Attribute && node.Label.Contains("id"));
        Assert.Contains(root.Children, node => node.Kind == XmlTreeNodeKind.Comment);
        Assert.Contains(root.Children, node => node.Kind == XmlTreeNodeKind.CData);
    }

    [Fact]
    public void ReportsLineAndPositionForMalformedXml()
    {
        var result = XmlTreeParser.Parse("<root>\n<item></root>");

        Assert.False(result.IsValid);
        Assert.Contains("行 2", result.ErrorMessage);
        Assert.Contains("位置", result.ErrorMessage);
    }

    [Fact]
    public void ProhibitsDtdAndExternalEntityExpansion()
    {
        var result = XmlTreeParser.Parse("<!DOCTYPE root [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><root>&xxe;</root>");

        Assert.False(result.IsValid);
        Assert.Contains("DTD", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
