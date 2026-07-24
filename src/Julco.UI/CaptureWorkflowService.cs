using System.IO;
namespace Julco.UI;

public sealed class CaptureWorkflowService
{
    public string SaveEvidencePackage(CaptureWorkflowRequest request)
    {
        Directory.CreateDirectory(request.CaptureRootDirectory);
        var captureDirectory = UniqueDirectory(Path.Combine(request.CaptureRootDirectory, request.FolderName));
        Directory.CreateDirectory(captureDirectory);

        File.WriteAllBytes(Path.Combine(captureDirectory, "screenshot.png"), request.ScreenshotBytes);
        File.WriteAllText(Path.Combine(captureDirectory, "inspection.json"), request.InspectionJson);
        File.WriteAllText(Path.Combine(captureDirectory, "dom.html"), request.DomHtml);
        File.WriteAllText(Path.Combine(captureDirectory, "computed.css"), request.ComputedCss);
        File.WriteAllText(Path.Combine(captureDirectory, "console.txt"), request.ConsoleText);
        File.WriteAllText(Path.Combine(captureDirectory, "attributes.txt"), request.AttributesText);
        File.WriteAllText(Path.Combine(captureDirectory, "image-resources.json"), request.ImagesJson);
        File.WriteAllText(Path.Combine(captureDirectory, "common-issues.json"), request.CommonIssuesJson);
        File.WriteAllText(Path.Combine(captureDirectory, "common-issues.md"), request.CommonIssuesMarkdown);

        CaptureNotesStore.Save(captureDirectory, request.Notes);
        File.WriteAllText(Path.Combine(captureDirectory, "evidence.json"), request.EvidenceJson);
        File.WriteAllText(Path.Combine(captureDirectory, "evidence-summary.md"), request.EvidenceMarkdown);
        File.WriteAllText(Path.Combine(captureDirectory, "manifest.json"), request.ManifestJson);

        return captureDirectory;
    }

    public static string UniqueDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return path;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{path}-{index}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}

public sealed record CaptureWorkflowRequest(
    string CaptureRootDirectory,
    string FolderName,
    byte[] ScreenshotBytes,
    string InspectionJson,
    string DomHtml,
    string ComputedCss,
    string ConsoleText,
    string AttributesText,
    string ImagesJson,
    string CommonIssuesJson,
    string CommonIssuesMarkdown,
    string EvidenceJson,
    string EvidenceMarkdown,
    string ManifestJson,
    CaptureNotes Notes);
