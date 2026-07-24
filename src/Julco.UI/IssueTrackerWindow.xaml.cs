using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Julco.Core.Configuration;

namespace Julco.UI;

public partial class IssueTrackerWindow : Window
{
    private readonly IReadOnlyList<IssueTrackerDraft> _drafts;
    private readonly string _outputDirectory;
    private readonly IssueTrackerSettings _settings;
    private readonly IssueTrackerClient _client = new();

    public IssueTrackerWindow(
        IReadOnlyList<IssueTrackerDraft> drafts,
        string outputDirectory,
        IssueTrackerSettings settings)
    {
        InitializeComponent();
        _drafts = drafts;
        _outputDirectory = outputDirectory;
        _settings = settings.Normalized();
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
            IntegrationStatusTextBlock.Text = "No issue draft selected.";
            SubmitButton.IsEnabled = false;
            return;
        }

        TitleTextBox.Text = draft.Title;
        BodyTextBox.Text = draft.Body;
        SavedPathTextBox.Text = draft.FilePath;
        IntegrationStatusTextBlock.Text = IssueTrackerClient.BuildConfigurationHint(draft, _settings);
        SubmitButton.IsEnabled = IssueTrackerClient.CanSubmit(draft, _settings);
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

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDraft is not { } draft)
        {
            return;
        }

        SubmitButton.IsEnabled = false;
        IntegrationStatusTextBlock.Text = $"Submitting to {draft.Name}...";
        try
        {
            var result = await _client.SubmitAsync(draft, _settings, CancellationToken.None);
            IntegrationStatusTextBlock.Text = result.Url is null
                ? result.Message
                : $"{result.Message} {result.Url}";

            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Url))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = result.Url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception exception)
        {
            IntegrationStatusTextBlock.Text = $"Submission failed: {exception.Message}";
        }
        finally
        {
            SubmitButton.IsEnabled = IssueTrackerClient.CanSubmit(draft, _settings);
        }
    }
}
