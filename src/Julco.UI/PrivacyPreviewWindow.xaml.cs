using System.IO;
using System.Text;
using System.Windows;

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
}
