using System.Windows;
using Julco.Core.Configuration;
using Forms = System.Windows.Forms;

namespace Julco.UI;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, string resolvedCaptureDirectory)
    {
        InitializeComponent();
        Settings = settings;
        CdpPortTextBox.Text = settings.Ui.CdpPort.ToString();
        CaptureDirectoryTextBox.Text = string.IsNullOrWhiteSpace(settings.Capture.ScreenshotDirectory)
            ? resolvedCaptureDirectory
            : settings.Capture.ScreenshotDirectory;
        FilePatternTextBox.Text = settings.Capture.FileNamePattern;
        HistoryMaxTextBox.Text = settings.History.MaxEntries.ToString();
        LensDelayTextBox.Text = settings.Ui.LensInspectionDelayMs.ToString();
        TopmostCheckBox.IsChecked = settings.Ui.KeepResultWindowsTopmost;
    }

    public AppSettings Settings { get; private set; }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose the Julco capture folder",
            SelectedPath = CaptureDirectoryTextBox.Text
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            CaptureDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(CdpPortTextBox.Text, out var port) || port <= 0 || port > 65535)
        {
            ShowValidation("CDP port must be between 1 and 65535.");
            return;
        }

        if (!int.TryParse(HistoryMaxTextBox.Text, out var historyMax) || historyMax < 1 || historyMax > 500)
        {
            ShowValidation("History max must be between 1 and 500.");
            return;
        }

        if (!int.TryParse(LensDelayTextBox.Text, out var lensDelay) || lensDelay < 150 || lensDelay > 5000)
        {
            ShowValidation("Lens delay must be between 150 and 5000 ms.");
            return;
        }

        var captureDirectory = CaptureDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            ShowValidation("Capture folder cannot be empty.");
            return;
        }

        var pattern = FilePatternTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            ShowValidation("File pattern cannot be empty.");
            return;
        }

        Settings = Settings with
        {
            Capture = Settings.Capture with
            {
                ScreenshotDirectory = captureDirectory,
                FileNamePattern = pattern
            },
            History = Settings.History with
            {
                MaxEntries = historyMax
            },
            Ui = Settings.Ui with
            {
                CdpPort = port,
                LensInspectionDelayMs = lensDelay,
                KeepResultWindowsTopmost = TopmostCheckBox.IsChecked == true
            }
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowValidation(string message)
    {
        System.Windows.MessageBox.Show(this, message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
