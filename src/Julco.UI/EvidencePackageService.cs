using System.IO;
using System.Text.Json;

namespace Julco.UI;

public sealed class EvidencePackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public EvidenceValidationResult Validate(string captureDirectory)
    {
        var issues = new List<EvidenceValidationIssue>();
        var evidence = LoadJson<EvidencePackage>(Path.Combine(captureDirectory, "evidence.json"));
        var manifest = LoadJson<CaptureManifest>(Path.Combine(captureDirectory, "manifest.json"));
        var schemaVersion = evidence?.Version
            ?? manifest?.SchemaVersion
            ?? "legacy";

        if (evidence is null)
        {
            issues.Add(new EvidenceValidationIssue("Error", "missing-evidence", "evidence.json is missing or unreadable.", "evidence.json"));
        }
        else if (!string.Equals(evidence.Version, EvidenceSchemaVersion.Current, StringComparison.Ordinal))
        {
            issues.Add(new EvidenceValidationIssue("Warning", "old-schema", $"Evidence schema is {evidence.Version}; current is {EvidenceSchemaVersion.Current}.", "evidence.json"));
        }

        if (manifest is null)
        {
            issues.Add(new EvidenceValidationIssue("Warning", "missing-manifest", "manifest.json is missing or unreadable.", "manifest.json"));
        }
        else if (!string.Equals(manifest.SchemaVersion, EvidenceSchemaVersion.Current, StringComparison.Ordinal))
        {
            issues.Add(new EvidenceValidationIssue("Warning", "old-manifest-schema", $"Manifest schema is {manifest.SchemaVersion ?? "legacy"}; current is {EvidenceSchemaVersion.Current}.", "manifest.json"));
        }

        var files = evidence?.Files ?? BuildFallbackFiles(manifest);
        ValidateFile(captureDirectory, files.Screenshot, "screenshot", issues, allowEmpty: false, canRepair: false);
        ValidateFile(captureDirectory, files.Inspection, "inspection-json", issues, allowEmpty: false, canRepair: true);
        ValidateFile(captureDirectory, files.Dom, "dom", issues, allowEmpty: false, canRepair: true);
        ValidateFile(captureDirectory, files.ComputedCss, "computed-css", issues, allowEmpty: true, canRepair: true);
        ValidateFile(captureDirectory, files.Console, "console", issues, allowEmpty: true, canRepair: true);
        ValidateFile(captureDirectory, files.Attributes, "attributes", issues, allowEmpty: true, canRepair: true);
        ValidateFile(captureDirectory, files.Images, "images", issues, allowEmpty: true, canRepair: true);
        ValidateFile(captureDirectory, files.StructuredNotes, "structured-notes", issues, allowEmpty: true, canRepair: true);
        ValidateFile(captureDirectory, files.Notes, "notes", issues, allowEmpty: true, canRepair: true);
        ValidateFile(captureDirectory, files.Summary, "summary", issues, allowEmpty: false, canRepair: true);

        return new EvidenceValidationResult(
            string.IsNullOrWhiteSpace(schemaVersion) ? "legacy" : schemaVersion,
            string.Equals(schemaVersion, EvidenceSchemaVersion.Current, StringComparison.Ordinal),
            issues);
    }

    public EvidenceRepairResult Repair(string captureDirectory)
    {
        var before = Validate(captureDirectory);
        var actions = new List<string>();
        Directory.CreateDirectory(captureDirectory);

        var evidencePath = Path.Combine(captureDirectory, "evidence.json");
        var manifestPath = Path.Combine(captureDirectory, "manifest.json");
        var evidence = LoadJson<EvidencePackage>(evidencePath);
        var manifest = LoadJson<CaptureManifest>(manifestPath);

        if (evidence is null && manifest is not null)
        {
            evidence = BuildEvidenceFromManifest(captureDirectory, manifest);
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, JsonOptions));
            actions.Add("Migrated legacy manifest into evidence.json.");
        }

        if (evidence is not null)
        {
            var migratedEvidence = evidence with
            {
                Version = EvidenceSchemaVersion.Current,
                Files = NormalizeFiles(evidence.Files),
                StructuredNotes = evidence.StructuredNotes ?? CaptureNotesStore.Load(captureDirectory)
            };

            File.WriteAllText(evidencePath, JsonSerializer.Serialize(migratedEvidence, JsonOptions));
            actions.Add("Rewrote evidence.json using the current schema.");
            evidence = migratedEvidence;

            var currentManifest = CaptureManifest.CreateCurrent(
                evidence.CreatedAt,
                evidence.Page.Title,
                evidence.Page.Url,
                evidence.Element.TagName,
                evidence.Element.Selector,
                evidence.Frame.X,
                evidence.Frame.Y,
                evidence.Frame.Width,
                evidence.Frame.Height,
                evidence.Files.Screenshot,
                evidence.Files.Inspection);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(currentManifest, JsonOptions));
            actions.Add("Rebuilt manifest.json using the current schema.");

            RepairFiles(captureDirectory, evidence, actions);
            File.WriteAllText(
                Path.Combine(captureDirectory, evidence.Files.Summary),
                BuildEvidenceMarkdown(evidence));
            actions.Add("Rebuilt evidence-summary.md.");
        }
        else
        {
            RepairMinimalLegacyFiles(captureDirectory, actions);
        }

        var after = Validate(captureDirectory);
        return new EvidenceRepairResult(before, after, actions, captureDirectory);
    }

    public static string BuildEvidenceMarkdown(EvidencePackage evidence)
    {
        var lines = new List<string>
        {
            "# Julco Evidence Package",
            string.Empty,
            "## Summary",
            $"- Schema: {NormalizeMarkdownLine(evidence.Version)}",
            $"- Created: {evidence.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"- Browser: {evidence.Browser.Name}",
            $"- Remote port: {evidence.Browser.RemotePort}",
            $"- Page title: {NormalizeMarkdownLine(evidence.Page.Title)}",
            $"- URL: {NormalizeMarkdownLine(evidence.Page.Url)}",
            $"- Element: {evidence.Element.TagName}  {NormalizeMarkdownLine(evidence.Element.Selector)}",
            $"- Detected type: {NormalizeMarkdownLine(evidence.Element.DetectedType)}",
            $"- Lens frame: {evidence.Frame.Width:0}x{evidence.Frame.Height:0} at {evidence.Frame.X:0},{evidence.Frame.Y:0}",
            $"- Center: {evidence.Frame.CenterX:0},{evidence.Frame.CenterY:0}",
            $"- Screen: {NormalizeMarkdownLine(evidence.Frame.ScreenName)} ({evidence.Frame.ScreenWidth}x{evidence.Frame.ScreenHeight})",
            string.Empty,
            "## Notes",
            BuildNotesMarkdown(evidence),
            string.Empty,
            "## Files",
            $"- Screenshot: `{evidence.Files.Screenshot}`",
            $"- Full inspection JSON: `{evidence.Files.Inspection}`",
            $"- DOM: `{evidence.Files.Dom}`",
            $"- Computed CSS: `{evidence.Files.ComputedCss}`",
            $"- Console: `{evidence.Files.Console}`",
            $"- Attributes: `{evidence.Files.Attributes}`",
            $"- Image resources: `{evidence.Files.Images}`",
            $"- Structured notes: `{evidence.Files.StructuredNotes}`",
            $"- Notes: `{evidence.Files.Notes}`",
            string.Empty,
            "## Element Attributes"
        };

        if (evidence.Element.Attributes.Count == 0)
        {
            lines.Add("_No attributes captured._");
        }
        else
        {
            lines.AddRange(evidence.Element.Attributes.Select(item =>
                $"- `{NormalizeMarkdownLine(item.Key)}`: {NormalizeMarkdownLine(item.Value)}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static EvidencePackage BuildEvidenceFromManifest(string captureDirectory, CaptureManifest manifest)
    {
        var notes = CaptureNotesStore.Load(captureDirectory);
        return new EvidencePackage(
            EvidenceSchemaVersion.Current,
            manifest.CreatedAt == default ? new DateTimeOffset(Directory.GetCreationTime(captureDirectory)) : manifest.CreatedAt,
            new EvidenceBrowserContext("Unknown", "-", "-", "-"),
            new EvidencePageContext(manifest.PageTitle ?? Path.GetFileName(captureDirectory), manifest.Url ?? "-"),
            new EvidenceElementContext(manifest.TagName ?? "-", manifest.Selector ?? "-", "-", new Dictionary<string, string>(), 0, 0),
            new EvidenceFrameContext(
                manifest.X,
                manifest.Y,
                manifest.Width,
                manifest.Height,
                manifest.X + manifest.Width / 2,
                manifest.Y + manifest.Height / 2,
                "Unknown",
                0,
                0),
            NormalizeFiles(BuildFallbackFiles(manifest)),
            notes.ToEvidenceText(),
            notes);
    }

    private static EvidenceFiles BuildFallbackFiles(CaptureManifest? manifest)
    {
        return new EvidenceFiles(
            manifest?.Screenshot ?? "screenshot.png",
            manifest?.Inspection ?? "inspection.json",
            "dom.html",
            "computed.css",
            "console.txt",
            "attributes.txt",
            "image-resources.json",
            "capture-notes.json",
            "notes.md",
            "evidence-summary.md");
    }

    private static EvidenceFiles NormalizeFiles(EvidenceFiles files)
    {
        return files with
        {
            Screenshot = DefaultIfBlank(files.Screenshot, "screenshot.png"),
            Inspection = DefaultIfBlank(files.Inspection, "inspection.json"),
            Dom = DefaultIfBlank(files.Dom, "dom.html"),
            ComputedCss = DefaultIfBlank(files.ComputedCss, "computed.css"),
            Console = DefaultIfBlank(files.Console, "console.txt"),
            Attributes = DefaultIfBlank(files.Attributes, "attributes.txt"),
            Images = DefaultIfBlank(files.Images, "image-resources.json"),
            StructuredNotes = DefaultIfBlank(files.StructuredNotes, "capture-notes.json"),
            Notes = DefaultIfBlank(files.Notes, "notes.md"),
            Summary = DefaultIfBlank(files.Summary, "evidence-summary.md")
        };
    }

    private static void RepairFiles(string captureDirectory, EvidencePackage evidence, List<string> actions)
    {
        EnsureTextFile(captureDirectory, evidence.Files.Inspection, "{}", actions);
        EnsureTextFile(captureDirectory, evidence.Files.Dom, "<!-- Julco repair placeholder: original DOM was missing. -->", actions);
        EnsureTextFile(captureDirectory, evidence.Files.ComputedCss, "/* Julco repair placeholder: computed CSS was missing. */", actions);
        EnsureTextFile(captureDirectory, evidence.Files.Console, string.Empty, actions);
        EnsureTextFile(captureDirectory, evidence.Files.Attributes, string.Empty, actions);
        EnsureTextFile(captureDirectory, evidence.Files.Images, "[]", actions);
        if (evidence.StructuredNotes is not null)
        {
            CaptureNotesStore.Save(captureDirectory, evidence.StructuredNotes);
        }
        else
        {
            EnsureTextFile(captureDirectory, evidence.Files.StructuredNotes, "{}", actions);
        }

        EnsureTextFile(captureDirectory, evidence.Files.Notes, evidence.StructuredNotes?.ToEvidenceText() ?? evidence.Notes, actions);
    }

    private static void RepairMinimalLegacyFiles(string captureDirectory, List<string> actions)
    {
        EnsureTextFile(captureDirectory, "inspection.json", "{}", actions);
        EnsureTextFile(captureDirectory, "dom.html", "<!-- Julco repair placeholder: original DOM was missing. -->", actions);
        EnsureTextFile(captureDirectory, "computed.css", "/* Julco repair placeholder: computed CSS was missing. */", actions);
        EnsureTextFile(captureDirectory, "console.txt", string.Empty, actions);
        EnsureTextFile(captureDirectory, "attributes.txt", string.Empty, actions);
        EnsureTextFile(captureDirectory, "image-resources.json", "[]", actions);
    }

    private static void ValidateFile(
        string captureDirectory,
        string relativePath,
        string code,
        List<EvidenceValidationIssue> issues,
        bool allowEmpty,
        bool canRepair)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            issues.Add(new EvidenceValidationIssue(canRepair ? "Warning" : "Error", $"missing-path-{code}", $"Missing path for {code}.", null));
            return;
        }

        var path = Path.Combine(captureDirectory, relativePath);
        if (!File.Exists(path))
        {
            issues.Add(new EvidenceValidationIssue(canRepair ? "Warning" : "Error", $"missing-file-{code}", $"Missing {relativePath}.", relativePath));
            return;
        }

        if (!allowEmpty && new FileInfo(path).Length == 0)
        {
            issues.Add(new EvidenceValidationIssue(canRepair ? "Warning" : "Error", $"empty-file-{code}", $"{relativePath} is empty.", relativePath));
        }
    }

    private static void EnsureTextFile(string captureDirectory, string relativePath, string content, List<string> actions)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var path = Path.Combine(captureDirectory, relativePath);
        var existed = File.Exists(path);
        if (existed && new FileInfo(path).Length > 0)
        {
            return;
        }

        File.WriteAllText(path, content);
        actions.Add(existed ? $"Repaired {relativePath}." : $"Created {relativePath}.");
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string BuildNotesMarkdown(EvidencePackage evidence)
    {
        if (evidence.StructuredNotes is { HasContent: true } notes)
        {
            return string.Join(
                Environment.NewLine,
                $"- Category: {NormalizeMarkdownLine(notes.Category)}",
                $"- Severity: {NormalizeMarkdownLine(notes.Severity)}",
                $"- Status: {NormalizeMarkdownLine(notes.Status)}",
                $"- Tags: {NormalizeMarkdownLine(notes.Tags)}",
                string.Empty,
                NormalizeMarkdownLine(notes.Observation));
        }

        return string.IsNullOrWhiteSpace(evidence.Notes)
            ? "_No notes added._"
            : NormalizeMarkdownLine(evidence.Notes);
    }

    private static string NormalizeMarkdownLine(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.ReplaceLineEndings(" ").Trim();
    }

    private static string DefaultIfBlank(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
