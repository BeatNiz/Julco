using System.Windows;
using System.Windows.Controls;

namespace Julco.UI;

public partial class EvidenceNotesWindow : Window
{
    private static readonly string[] Categories =
    {
        "Visual issue",
        "Layout",
        "Image",
        "Text",
        "Accessibility",
        "Behavior",
        "Performance",
        "Other"
    };

    private static readonly string[] Severities =
    {
        "Low",
        "Medium",
        "High",
        "Critical"
    };

    private static readonly string[] Statuses =
    {
        "Open",
        "Needs review",
        "Confirmed",
        "Fixed",
        "Won't fix"
    };

    private static readonly NoteTemplate[] Templates =
    {
        new("Misaligned button", "Layout", "Medium", "layout, button", "The button appears misaligned against the surrounding controls."),
        new("Incorrect image", "Image", "High", "image, asset", "The framed image does not match the expected asset or appears to be the wrong resource."),
        new("Invisible text", "Text", "High", "text, contrast", "The text is present but is hard to read or visually invisible against the background."),
        new("Overflow", "Layout", "Medium", "layout, overflow", "The content overflows its container or is clipped in the framed region."),
        new("Missing alt text", "Accessibility", "Medium", "accessibility, image", "The image appears to need alternative text or an accessible label."),
        new("Unexpected console message", "Behavior", "Medium", "console, runtime", "The capture includes console messages that may explain the visual or behavioral issue.")
    };

    public EvidenceNotesWindow(CaptureNotes notes)
    {
        InitializeComponent();

        CategoryComboBox.ItemsSource = Categories;
        SeverityComboBox.ItemsSource = Severities;
        StatusComboBox.ItemsSource = Statuses;
        TemplatesListBox.ItemsSource = Templates;
        TemplatesListBox.DisplayMemberPath = nameof(NoteTemplate.Title);

        CategoryComboBox.SelectedItem = SelectOrDefault(Categories, notes.Category);
        SeverityComboBox.SelectedItem = SelectOrDefault(Severities, notes.Severity);
        StatusComboBox.SelectedItem = SelectOrDefault(Statuses, notes.Status);
        TagsTextBox.Text = notes.Tags;
        ObservationTextBox.Text = notes.Observation;
        ObservationTextBox.Focus();
        ObservationTextBox.CaretIndex = ObservationTextBox.Text.Length;
    }

    public CaptureNotes Notes => new(
        ObservationTextBox.Text.Trim(),
        (CategoryComboBox.SelectedItem as string) ?? CaptureNotes.Empty.Category,
        (SeverityComboBox.SelectedItem as string) ?? CaptureNotes.Empty.Severity,
        (StatusComboBox.SelectedItem as string) ?? CaptureNotes.Empty.Status,
        TagsTextBox.Text.Trim(),
        DateTimeOffset.Now);

    private static string SelectOrDefault(IReadOnlyList<string> values, string requested)
    {
        return values.FirstOrDefault(value => string.Equals(value, requested, StringComparison.OrdinalIgnoreCase))
            ?? values[0];
    }

    private void TemplatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplatesListBox.SelectedItem is not NoteTemplate template)
        {
            return;
        }

        CategoryComboBox.SelectedItem = template.Category;
        SeverityComboBox.SelectedItem = template.Severity;
        TagsTextBox.Text = MergeTags(TagsTextBox.Text, template.Tags);
        ObservationTextBox.Text = string.IsNullOrWhiteSpace(ObservationTextBox.Text)
            ? template.Text
            : $"{ObservationTextBox.Text.Trim()}{Environment.NewLine}{Environment.NewLine}{template.Text}";
        ObservationTextBox.Focus();
        ObservationTextBox.CaretIndex = ObservationTextBox.Text.Length;
        TemplatesListBox.SelectedItem = null;
    }

    private static string MergeTags(string currentTags, string templateTags)
    {
        return string.Join(
            ", ",
            currentTags
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(templateTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed record NoteTemplate(
        string Title,
        string Category,
        string Severity,
        string Tags,
        string Text);
}
