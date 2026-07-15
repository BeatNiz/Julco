using System.Windows;

namespace Julco.UI;

public partial class ResultWindow : Window
{
    public ResultWindow(string title, string content)
    {
        InitializeComponent();
        Title = $"Julco - {title}";
        TitleTextBlock.Text = title;
        ContentTextBox.Text = content;
    }
}
