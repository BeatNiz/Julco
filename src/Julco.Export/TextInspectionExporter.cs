using Julco.Core.Exporting;
using Julco.Core.Inspection;

namespace Julco.Export;

public sealed class TextInspectionExporter : IInspectionExporter
{
    public TextInspectionExporter(ExportFormat format)
    {
        Format = format;
    }

    public ExportFormat Format { get; }

    public ExportPackage Export(InspectionResult inspection)
    {
        return Format switch
        {
            ExportFormat.OuterHtml => Text(".html", "text/html", inspection.Dom.OuterHtml ?? string.Empty),
            ExportFormat.ComputedCss => Text(".css", "text/css", BuildComputedCss(inspection.Css.Computed)),
            ExportFormat.CssRules => Text(".css", "text/css", BuildRules(inspection.Css.MatchedRules)),
            ExportFormat.XPath => Text(".txt", "text/plain", inspection.Element.XPath ?? string.Empty),
            ExportFormat.CssSelector => Text(".txt", "text/plain", inspection.Element.CssSelector ?? string.Empty),
            ExportFormat.Accessibility => Text(".txt", "text/plain", BuildAccessibility(inspection.Accessibility)),
            ExportFormat.RuntimeConsole => Text(".txt", "text/plain", BuildRuntimeConsole(inspection.RuntimeConsole)),
            _ => throw new NotSupportedException($"The export format '{Format}' is not supported by this exporter.")
        };
    }

    private ExportPackage Text(string extension, string mimeType, string content)
    {
        return new ExportPackage(Format, extension, mimeType, content);
    }

    private static string BuildComputedCss(IEnumerable<CssDeclaration> declarations)
    {
        return string.Join(
            Environment.NewLine,
            declarations.Select(item => $"{item.Name}: {item.Value}{(item.IsImportant ? " !important" : string.Empty)};"));
    }

    private static string BuildRules(IEnumerable<CssRule> rules)
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            rules.Select(rule =>
                $"{rule.Selector} {{{Environment.NewLine}"
                + string.Join(Environment.NewLine, rule.Declarations.Select(item => $"    {item.Name}: {item.Value};"))
                + $"{Environment.NewLine}}}"));
    }

    private static string BuildAccessibility(AccessibilitySnapshot? accessibility)
    {
        if (accessibility is null)
        {
            return string.Empty;
        }

        return $"role: {accessibility.Role}{Environment.NewLine}name: {accessibility.Name}";
    }

    private static string BuildRuntimeConsole(RuntimeConsoleSnapshot? runtimeConsole)
    {
        if (runtimeConsole is null)
        {
            return string.Empty;
        }

        var messages = runtimeConsole.Messages.Select(message =>
            $"[{message.Level}] {message.Text} ({message.Url}:{message.Line}:{message.Column})");

        var exceptions = runtimeConsole.Exceptions.Select(exception =>
            $"[Exception] {exception.Text} ({exception.Url}:{exception.Line}:{exception.Column})");

        return string.Join(Environment.NewLine, messages.Concat(exceptions));
    }
}
