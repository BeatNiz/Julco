using System.Windows;

namespace Julco.UI;

public partial class EvidenceNotesWindow : Window
{
    public EvidenceNotesWindow(string notes = "")
    {
        InitializeComponent();
        NotesTextBox.Text = notes;
        NotesTextBox.Focus();
        NotesTextBox.CaretIndex = NotesTextBox.Text.Length;
    }

    public string Notes => NotesTextBox.Text.Trim();

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
}
