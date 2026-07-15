using Julco.Core.Geometry;
using Julco.Core.Inspection;

namespace Julco.Cdp;

public abstract class ChromiumCdpBrowserAdapter : IBrowserAdapter
{
    public abstract BrowserKind BrowserKind { get; }

    public BrowserAdapterCapabilities Capabilities { get; } = new(
        SupportsPointInspection: true,
        SupportsRegionInspection: false,
        SupportsComputedStyle: true,
        SupportsMatchedCss: true,
        SupportsAccessibilityTree: true,
        SupportsRuntimeConsole: true,
        SupportsElementCapture: true,
        SupportsNodeHighlight: true);

    public Task<IReadOnlyList<BrowserTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException("CDP target discovery belongs to the next implementation step.");
    }

    public Task ConnectAsync(BrowserTarget target, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("CDP WebSocket connection belongs to the next implementation step.");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException("CDP disconnection belongs to the next implementation step.");
    }

    public Task<InspectionResult?> InspectAsync(
        InspectionRequest request,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("DOM.getNodeForLocation and Runtime/Log collection belong to the next implementation step.");
    }

    public Task<IReadOnlyList<InspectedElement>> FindElementsInRegionAsync(
        ScreenRect region,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Region inspection belongs after point inspection is validated.");
    }

    public Task HighlightNodeAsync(ElementHandle handle, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Overlay.highlightNode belongs to the next implementation step.");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
