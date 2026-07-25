using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Julco.Core.Configuration;

namespace Julco.UI;

public partial class IssueTrackerWindow : Window
{
    private readonly IReadOnlyList<IssueTrackerDraft> _drafts;
    private readonly string _outputDirectory;
    private readonly IssueTrackerSettings _settings;
    private readonly PrivacyPreviewModel _privacyPreview;
    private readonly IssueTrackerClient _client = new();

    public IssueTrackerWindow(
        IReadOnlyList<IssueTrackerDraft> drafts,
        string outputDirectory,
        IssueTrackerSettings settings,
        PrivacyPreviewModel privacyPreview)
    {
        InitializeComponent();
        _drafts = drafts;
        _outputDirectory = outputDirectory;
        _settings = settings.Normalized();
        _privacyPreview = privacyPreview;
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
        IntegrationStatusTextBlock.Text = BuildStatusText(draft);
        SubmitButton.IsEnabled = IssueTrackerClient.CanSubmit(draft, _settings);
    }

    private void CopyTitleButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedDraft is not { } draft)
        {
            return;
        }

        if (!ConfirmSensitiveScreenshotSubmission(draft))
        {
            return;
        }

        System.Windows.Clipboard.SetText(draft.Title);
    }

    private string BuildStatusText(IssueTrackerDraft draft)
    {
        var status = IssueTrackerClient.BuildConfigurationHint(draft, _settings);
        if (_privacyPreview.ScreenshotRisk && _privacyPreview.PrivacySettings.WarnBeforeSendingSensitiveScreenshots)
        {
            status += " Screenshot warning: the safe package includes an unredacted screenshot.";
        }

        if (_privacyPreview.HasChanges)
        {
            status += $" Privacy preview: {_privacyPreview.Summary.TotalMatches} redaction finding(s) handled in draft text.";
        }

        return status;
    }

    private bool ConfirmSensitiveScreenshotSubmission(IssueTrackerDraft draft)
    {
        if (!_privacyPreview.ScreenshotRisk
            || !_privacyPreview.PrivacySettings.WarnBeforeSendingSensitiveScreenshots
            || draft.Provider is not (IssueTrackerProvider.GitHub or IssueTrackerProvider.Jira))
        {
            return true;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            "The current privacy settings include an unredacted screenshot in safe exports. Screenshots can contain visible private data. Continue submitting?",
            "Sensitive screenshot warning",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
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
            SaveSubmissionResult(draft, result);
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

    private void SaveSubmissionResult(IssueTrackerDraft draft, IssueTrackerSubmissionResult result)
    {
        Directory.CreateDirectory(_outputDirectory);
        var record = new IssueTrackerSubmissionRecord(
            DateTimeOffset.Now,
            result.Provider,
            draft.Title,
            result.Succeeded,
            result.Message,
            result.Url,
            _outputDirectory);
        File.WriteAllText(
            Path.Combine(_outputDirectory, "last-submission.json"),
            JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
    }
}
