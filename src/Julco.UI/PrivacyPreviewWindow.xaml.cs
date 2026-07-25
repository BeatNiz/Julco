using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Julco.UI;

public partial class PrivacyPreviewWindow : Window
{
    private readonly PrivacyPreviewModel _model;
    private readonly Func<PrivacyPreviewModel, string> _exportSafePackage;

    public PrivacyPreviewWindow(PrivacyPreviewModel model, Func<PrivacyPreviewModel, string> exportSafePackage)
    {
        InitializeComponent();
        _model = model;
        _exportSafePackage = exportSafePackage;
        StatusTextBlock.Text = model.HasChanges
            ? "Review the safe sample before sharing. Redacted exports use the right column."
            : "No configured sensitive patterns were found. Safe export will still keep data in a separate package.";
        SummaryTextBox.Text = model.SummaryText;
        OriginalTextBox.Text = model.OriginalPreview;
        RedactedTextBox.Text = model.RedactedPreview;
        FieldPreviewListBox.ItemsSource = model.FieldPreviews;
        FieldPreviewListBox.SelectedIndex = model.FieldPreviews.Count > 0 ? 0 : -1;
        ScreenshotStatusTextBlock.Text = model.ScreenshotRisk
            ? "Screenshot will be included without redaction. Review visible content before sharing or sending."
            : model.IncludeScreenshotInSafeExport
                ? model.ScreenshotWillBeRedacted
                    ? "Screenshot will be included as a redacted copy."
                    : "Screenshot will be included."
                : "Screenshot is omitted from safe export.";
        LoadScreenshotPreview();
    }

    private void FieldPreviewListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FieldPreviewListBox.SelectedItem is not Julco.Core.Privacy.PrivacyRedactionFieldPreview field)
        {
            return;
        }

        OriginalTextBox.Text = field.Before;
        RedactedTextBox.Text = field.After;
    }

    private void CopySummaryButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Clipboard.SetText(SummaryTextBox.Text);
    }

    private void ExportSafeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = _exportSafePackage(_model);
            StatusTextBlock.Text = $"Safe package exported: {directory}";
            if (Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"Safe export failed: {exception.Message}";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadScreenshotPreview()
    {
        if (string.IsNullOrWhiteSpace(_model.Original.ScreenshotPath)
            || !File.Exists(_model.Original.ScreenshotPath))
        {
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(_model.Original.ScreenshotPath);
        bitmap.EndInit();
        bitmap.Freeze();
        ScreenshotPreviewImage.Source = bitmap;
    }
}
