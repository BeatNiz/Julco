using System.Windows;
using System.Windows.Controls;

namespace Julco.UI;

public partial class DomResultWindow : Window
{
    public DomResultWindow(string html)
    {
        InitializeComponent();
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
