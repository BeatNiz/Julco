using System.Reflection;
using System.Text.Json;

namespace Julco.Cdp;

internal static class InspectionRuntime
{
    private const string ResourceName = "Julco.Cdp.Resources.inspection-runtime.js";
    private static readonly Lazy<string> RuntimeSource = new(LoadRuntimeSource);

    public static string BuildSelectorExpression(string selector)
    {
        return BuildJsonExpression($"window.__julcoInspectionRuntime.inspectSelector({JsonSerializer.Serialize(selector)})");
    }

    public static string BuildScreenPointExpression(
        double screenX,
        double screenY,
        double regionLeft,
        double regionTop,
        double regionWidth,
        double regionHeight)
    {
        return BuildJsonExpression(
            "window.__julcoInspectionRuntime.inspectScreenPoint("
            + string.Join(
                ",",
                Format(screenX),
                Format(screenY),
                Format(regionLeft),
                Format(regionTop),
                Format(regionWidth),
                Format(regionHeight))
            + ")");
    }

    public static string BuildRuntimeInstallExpression()
    {
        return $"JSON.stringify({RuntimeSource.Value})";
    }

    private static string BuildJsonExpression(string callExpression)
    {
        return $$"""
            (() => {
                if (!window.__julcoInspectionRuntime) {
                    {{RuntimeSource.Value}};
                }

                return JSON.stringify({{callExpression}});
            })()
            """;
    }

    private static string Format(double value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string LoadRuntimeSource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded inspection runtime not found: {ResourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
