using System.Windows;

namespace Julco.UI;

public partial class CommonIssuesWindow : Window
{
    public CommonIssuesWindow(IReadOnlyList<CommonIssue> issues)
    {
        InitializeComponent();
        TitleTextBlock.Text = issues.Count == 0
            ? "No common issues detected"
            : $"{issues.Count} common issue(s) detected";
        IssuesGrid.ItemsSource = issues;
        IssuesTextBox.Text = CommonIssueDetector.BuildReport(issues);
    }
}
