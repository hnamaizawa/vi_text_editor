using System.Xml;
using System.Xml.Linq;

namespace ViTextEditor.Core.IO;

public enum XmlTreeNodeKind
{
    Document,
    Declaration,
    Element,
    Attribute,
    Text,
    CData,
    Comment,
    ProcessingInstruction
}

public sealed record XmlTreeNode(
    XmlTreeNodeKind Kind,
    string Label,
    IReadOnlyList<XmlTreeNode> Children);

public sealed record XmlTreeDocument(
    XmlTreeNode? Root,
    int ElementCount,
    string? ErrorMessage)
{
    public bool IsValid => Root is not null && ErrorMessage is null;
}

public static class XmlTreeParser
{
    private const int MaxCharacters = 16 * 1024 * 1024;

    public static XmlTreeDocument Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new XmlTreeDocument(null, 0, "XMLが空です。");

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxCharacters,
                IgnoreWhitespace = true
            };
            using var input = new StringReader(text);
            using var reader = XmlReader.Create(input, settings);
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var children = new List<XmlTreeNode>();
            if (document.Declaration is { } declaration)
            {
                var standalone = declaration.Standalone is null ? string.Empty : $" standalone=\"{declaration.Standalone}\"";
                children.Add(new XmlTreeNode(
                    XmlTreeNodeKind.Declaration,
                    $"<?xml version=\"{declaration.Version}\" encoding=\"{declaration.Encoding ?? "(unspecified)"}\"{standalone}?>",
                    []));
            }

            var elementCount = 0;
            foreach (var node in document.Nodes()) children.Add(ConvertNode(node, ref elementCount));
            var root = new XmlTreeNode(XmlTreeNodeKind.Document, "XML Document", children);
            return new XmlTreeDocument(root, elementCount, null);
        }
        catch (XmlException ex)
        {
            return new XmlTreeDocument(null, 0, $"XML解析エラー (行 {ex.LineNumber}, 位置 {ex.LinePosition}): {ex.Message}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new XmlTreeDocument(null, 0, $"XML解析エラー: {ex.Message}");
        }
    }

    private static XmlTreeNode ConvertNode(XNode node, ref int elementCount)
    {
        switch (node)
        {
            case XElement element:
            {
                elementCount++;
                var children = new List<XmlTreeNode>();
                foreach (var attribute in element.Attributes())
                {
                    children.Add(new XmlTreeNode(
                        XmlTreeNodeKind.Attribute,
                        $"@{DisplayName(attribute.Name)} = \"{Compact(attribute.Value)}\"",
                        []));
                }
                foreach (var child in element.Nodes())
                {
                    if (child is XText text && string.IsNullOrWhiteSpace(text.Value)) continue;
                    children.Add(ConvertNode(child, ref elementCount));
                }
                return new XmlTreeNode(XmlTreeNodeKind.Element, $"<{DisplayName(element.Name)}>", children);
            }
            case XCData cdata:
                return new XmlTreeNode(XmlTreeNodeKind.CData, $"<![CDATA[{Compact(cdata.Value)}]]>", []);
            case XText text:
                return new XmlTreeNode(XmlTreeNodeKind.Text, Compact(text.Value), []);
            case XComment comment:
                return new XmlTreeNode(XmlTreeNodeKind.Comment, $"<!-- {Compact(comment.Value)} -->", []);
            case XProcessingInstruction instruction:
                return new XmlTreeNode(
                    XmlTreeNodeKind.ProcessingInstruction,
                    $"<?{instruction.Target} {Compact(instruction.Data)}?>",
                    []);
            default:
                return new XmlTreeNode(XmlTreeNodeKind.Text, Compact(node.ToString()), []);
        }
    }

    private static string DisplayName(XName name) =>
        string.IsNullOrEmpty(name.NamespaceName) ? name.LocalName : $"{{{name.NamespaceName}}}{name.LocalName}";

    private static string Compact(string value)
    {
        var compact = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 240 ? compact : compact[..237] + "...";
    }
}
