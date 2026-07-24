using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Julco.UI;

public partial class IssueTrackerWindow : Window
{
    private readonly IReadOnlyList<IssueTrackerDraft> _drafts;
    private readonly string _outputDirectory;

    public IssueTrackerWindow(IReadOnlyList<IssueTrackerDraft> drafts, string outputDirectory)
    {
        InitializeComponent();
        _drafts = drafts;
        _outputDirectory = outputDirectory;
        DraftComboBox.ItemsSource = _drafts;
        DraftComboBox.SelectedIndex = _drafts.Count > 0 ? 0 : -1;
    }

    private IssueTrackerDraft? SelectedDraft => DraftComboBox.SelectedItem as IssueTrackerDraft;

    private void DraftComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedDraft is not { } draft)
        {
            TitleTextBox.Text = string.Empty;
            BodyTextBox.Text = string.Empty;
            SavedPathTextBox.Text = string.Empty;
            return;
        }

        TitleTextBox.Text = draft.Title;
        BodyTextBox.Text = draft.Body;
        SavedPathTextBox.Text = draft.FilePath;
    }

    private void CopyTitleButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDraft is not { } draft)
        {
            return;
        }

        System.Windows.Clipboard.SetText(draft.Title);
    }

    private void CopyBodyButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDraft is not { } draft)
        {
            return;
        }

        System.Windows.Clipboard.SetText(draft.Body);
    }

    private void CopyBothButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDraft is not { } draft)
        {
            return;
        }

        System.Windows.Clipboard.SetText($"{draft.Title}{Environment.NewLine}{Environment.NewLine}{draft.Body}");
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_outputDirectory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _outputDirectory,
            UseShellExecute = true
        });
    }
}
