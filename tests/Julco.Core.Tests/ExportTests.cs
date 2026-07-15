using Julco.Core.Exporting;
using Julco.Core.Geometry;
using Julco.Core.Inspection;
using Julco.Export;
using Xunit;

namespace Julco.Core.Tests;

public sealed class ExportTests
{
    [Fact]
    public void OuterHtmlExporterReturnsHtmlPackage()
    {
        var exporter = new TextInspectionExporter(ExportFormat.OuterHtml);

        var package = exporter.Export(CreateInspection());

        Assert.Equal(".html", package.FileExtension);
        Assert.Equal("<button>Save</button>", package.Content);
    }

    [Fact]
    public void ComputedCssExporterFormatsDeclarations()
    {
        var exporter = new TextInspectionExporter(ExportFormat.ComputedCss);

        var package = exporter.Export(CreateInspection());

        Assert.Contains("display: flex;", package.Content);
    }

    [Fact]
    public void DefaultExportServiceCanExportSelector()
    {
        var service = InspectionExportService.CreateDefault();

        var package = service.Export(CreateInspection(), ExportFormat.CssSelector);

        Assert.Equal("#save", package.Content);
    }

    [Fact]
    public void RuntimeConsoleExporterFormatsMessages()
    {
        var exporter = new TextInspectionExporter(ExportFormat.RuntimeConsole);

        var package = exporter.Export(CreateInspection(runtimeConsole: true));

        Assert.Contains("hydrated", package.Content);
    }

    private static InspectionResult CreateInspection(bool runtimeConsole = false)
    {
        var target = new BrowserTarget(
            "target-1",
            BrowserKind.Chrome,
            "Sample",
            new Uri("https://example.com"),
            ProcessId: 10,
            WindowBounds: new ScreenRect(0, 0, 800, 600),
            IsInspectable: true);

        var element = new InspectedElement(
            new ElementHandle("node-1", null, null),
            "button",
            "save",
            new[] { "primary" },
            new Dictionary<string, string>(),
            new ScreenRect(0, 0, 100, 40),
            new ScreenRect(0, 0, 100, 40),
            "#save",
            "#save",
            "/html/body/button",
            Depth: 2,
            ChildCount: 0,
            SiblingCount: 0);

        var css = new CssSnapshot(
            new[] { new CssDeclaration("display", "flex", false, null, false) },
            Array.Empty<CssRule>(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        var console = runtimeConsole
            ? new RuntimeConsoleSnapshot(
                new[]
                {
                    new RuntimeConsoleMessage(
                        RuntimeConsoleMessageLevel.Info,
                        "hydrated",
                        "console-api",
                        "https://example.com/app.js",
                        12,
                        4,
                        DateTimeOffset.UtcNow)
                },
                Array.Empty<RuntimeExceptionInfo>(),
                Array.Empty<RuntimeScriptInfo>(),
                WasRuntimeEvaluationUsed: false)
            : null;

        return new InspectionResult(
            target,
            element,
            new DomSnapshot("<button>Save</button>", "Save", "Save", Array.Empty<string>(), false, false, false, false),
            css,
            Accessibility: null,
            RuntimeConsole: console,
            CapturedAt: DateTimeOffset.UtcNow,
            Warnings: Array.Empty<InspectionWarning>());
    }
}
