using System.Windows;
using System.Windows.Controls;

namespace Julco.UI;

public partial class DomResultWindow : Window
{
    public DomResultWindow(string html, string tagName = "", string selector = "")
    {
        InitializeComponent();
        var attributes = DomFormatter.ExtractAttributes(html);
        var attributeDictionary = attributes.ToDictionary(
            attribute => attribute.Name,
            attribute => attribute.Value,
            StringComparer.OrdinalIgnoreCase);
        var summary = DomSummaryBuilder.Build(tagName, selector, html, attributeDictionary);
        DomSummaryTextBox.Text = DomSummaryBuilder.ToDisplayText(summary);
        ImportantAttributesGrid.ItemsSource = summary.ImportantAttributes;
        AttributesListView.ItemsSource = DomFormatter.ExtractAttributes(html);
        DomRichTextBox.Document = DomSyntaxHighlighter.CreateDocument(DomFormatter.PrettyPrint(html));
    }

    private void AttributesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AttributeHelpTextBlock.Text = AttributesListView.SelectedItem is DomAttributeView attribute
            ? $"{attribute.Name}: {attribute.Description}"
            : "Select an attribute to see what it is and why it matters.";
    }
}
