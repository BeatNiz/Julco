using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;

namespace Julco.UI;

public static class DomSyntaxHighlighter
{
    private static readonly MediaBrush Background = Brush("#050608");
    private static readonly MediaBrush PlainText = Brush("#E7EAF0");
    private static readonly MediaBrush Tag = Brush("#6CB6FF");
    private static readonly MediaBrush Attribute = Brush("#9CDCFE");
    private static readonly MediaBrush Value = Brush("#F4B183");
    private static readonly MediaBrush Symbol = Brush("#B8C0CC");
    private static readonly MediaBrush Comment = Brush("#7BD88F");

    private static readonly Regex TagRegex = new(
        @"(?<comment><!--.*?-->)|(?<tag></?)(?<name>[a-zA-Z][\w:-]*)(?<attrs>[^<>]*?)(?<end>/?>)",
        RegexOptions.Compiled);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[^\s=\/<>]+)(?<equals>\s*=\s*)?(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<bare>[^\s""'=<>`]+))?",
        RegexOptions.Compiled);

    public static FlowDocument CreateDocument(string formattedHtml)
    {
        var document = new FlowDocument
        {
            Background = Background,
            Foreground = PlainText,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            PagePadding = new System.Windows.Thickness(10)
        };

        foreach (var line in formattedHtml.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var paragraph = new Paragraph
            {
                Margin = new System.Windows.Thickness(0),
                LineHeight = 18
            };
            AddHighlightedLine(paragraph, line);
            document.Blocks.Add(paragraph);
        }

        return document;
    }

    private static void AddHighlightedLine(Paragraph paragraph, string line)
    {
        var match = TagRegex.Match(line);
        if (!match.Success)
        {
            paragraph.Inlines.Add(Run(line, PlainText));
            return;
        }

        if (match.Index > 0)
        {
            paragraph.Inlines.Add(Run(line[..match.Index], PlainText));
        }

        if (match.Groups["comment"].Success)
        {
            paragraph.Inlines.Add(Run(match.Groups["comment"].Value, Comment));
        }
        else
        {
            paragraph.Inlines.Add(Run(match.Groups["tag"].Value, Symbol));
            paragraph.Inlines.Add(Run(match.Groups["name"].Value, Tag));
            AddAttributes(paragraph, match.Groups["attrs"].Value);
            paragraph.Inlines.Add(Run(match.Groups["end"].Value, Symbol));
        }

        var restStart = match.Index + match.Length;
        if (restStart < line.Length)
        {
            paragraph.Inlines.Add(Run(line[restStart..], PlainText));
        }
    }

    private static void AddAttributes(Paragraph paragraph, string attributes)
    {
        var index = 0;
        foreach (Match match in AttributeRegex.Matches(attributes))
        {
            if (match.Index > index)
            {
                paragraph.Inlines.Add(Run(attributes[index..match.Index], PlainText));
            }

            paragraph.Inlines.Add(Run(match.Groups["name"].Value, Attribute));

            if (match.Groups["equals"].Success)
            {
                paragraph.Inlines.Add(Run(match.Groups["equals"].Value, Symbol));
            }

            if (match.Groups["value"].Success)
            {
                paragraph.Inlines.Add(Run($"\"{match.Groups["value"].Value}\"", Value));
            }
            else if (match.Groups["bare"].Success)
            {
                paragraph.Inlines.Add(Run(match.Groups["bare"].Value, Value));
            }

            index = match.Index + match.Length;
        }

        if (index < attributes.Length)
        {
            paragraph.Inlines.Add(Run(attributes[index..], PlainText));
        }
    }

    private static Run Run(string text, MediaBrush foreground)
    {
        return new Run(text)
        {
            Foreground = foreground
        };
    }

    private static SolidColorBrush Brush(string color)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }
}
