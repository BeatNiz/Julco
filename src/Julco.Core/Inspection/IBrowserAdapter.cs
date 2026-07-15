using Julco.Core.Geometry;

namespace Julco.Core.Inspection;

public interface IBrowserAdapter : IAsyncDisposable
{
    BrowserKind BrowserKind { get; }

    BrowserAdapterCapabilities Capabilities { get; }

    Task<IReadOnlyList<BrowserTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken);

    Task ConnectAsync(BrowserTarget target, CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    Task<InspectionResult?> InspectAsync(
        InspectionRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InspectedElement>> FindElementsInRegionAsync(
        ScreenRect region,
        CancellationToken cancellationToken);

    Task HighlightNodeAsync(
        ElementHandle handle,
        CancellationToken cancellationToken);
}
