using System.IO;
using System.Text.Json;

namespace Julco.UI;

public static class CaptureNotesStore
{
    public static CaptureNotes Load(string captureDirectory)
    {
        var structuredNotesPath = Path.Combine(captureDirectory, "capture-notes.json");
        if (File.Exists(structuredNotesPath))
        {
            try
            {
                var notes = JsonSerializer.Deserialize<CaptureNotes>(File.ReadAllText(structuredNotesPath));
                if (notes is not null)
                {
                    return notes;
                }
            }
            catch (JsonException)
            {
            }
        }

        var notesPath = Path.Combine(captureDirectory, "notes.md");
        if (File.Exists(notesPath))
        {
            var legacyNotes = ExtractObservationFromNotesMarkdown(File.ReadAllText(notesPath));
            if (!string.IsNullOrWhiteSpace(legacyNotes))
            {
                return CaptureNotes.Empty with
                {
                    Observation = legacyNotes,
                    UpdatedAt = new DateTimeOffset(File.GetLastWriteTime(notesPath))
                };
            }
        }

        return CaptureNotes.Empty;
    }

    public static void Save(string captureDirectory, CaptureNotes notes)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(captureDirectory, "capture-notes.json"),
            JsonSerializer.Serialize(notes, jsonOptions));
        File.WriteAllText(
            Path.Combine(captureDirectory, "notes.md"),
            notes.ToMarkdown());
    }

    private static string ExtractObservationFromNotesMarkdown(string content)
    {
        var text = content.Trim();
        const string observationHeader = "## Observation";
        var observationIndex = text.IndexOf(observationHeader, StringComparison.OrdinalIgnoreCase);
        if (observationIndex < 0)
        {
            return text;
        }

        return text[(observationIndex + observationHeader.Length)..]
            .Replace("_No observation added._", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
