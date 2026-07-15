using Julco.Core.Geometry;
using Julco.Core.Inspection;

namespace Julco.Browser;

public sealed class BrowserManager
{
    private readonly IReadOnlyList<IBrowserAdapter> _adapters;

    public BrowserManager(IEnumerable<IBrowserAdapter> adapters)
    {
        _adapters = adapters.ToArray();
    }

    public async Task<InspectionResult?> InspectAtPointAsync(
        BrowserKind browserKind,
        ScreenPoint point,
        CancellationToken cancellationToken)
    {
        var adapter = _adapters.FirstOrDefault(item => item.BrowserKind == browserKind);
        if (adapter is null)
        {
            return null;
        }

        var targets = await adapter.DiscoverTargetsAsync(cancellationToken);
        var target = SelectBestTarget(targets, point);

        if (target is null)
        {
            return null;
        }

        await adapter.ConnectAsync(target, cancellationToken);

        var request = new InspectionRequest(
            target,
            point,
            Region: null,
            Options: new InspectionOptions(),
            Trigger: InspectionTrigger.ManualPoint);

        return await adapter.InspectAsync(request, cancellationToken);
    }

    public async Task<InspectionResult?> InspectRegionAsync(
        BrowserKind browserKind,
        ScreenRect region,
        CancellationToken cancellationToken)
    {
        var centerPoint = new ScreenPoint(region.X + region.Width / 2, region.Y + region.Height / 2);
        var adapter = _adapters.FirstOrDefault(item => item.BrowserKind == browserKind);
        if (adapter is null)
        {
            return null;
        }

        var targets = await adapter.DiscoverTargetsAsync(cancellationToken);
        var target = SelectBestTarget(targets, centerPoint);

        if (target is null)
        {
            return null;
        }

        await adapter.ConnectAsync(target, cancellationToken);

        var request = new InspectionRequest(
            target,
            centerPoint,
            region,
            new InspectionOptions(),
            InspectionTrigger.LensRegion);

        return await adapter.InspectAsync(request, cancellationToken);
    }

    private static BrowserTarget? SelectBestTarget(
        IEnumerable<BrowserTarget> targets,
        ScreenPoint point)
    {
        var inspectableTargets = targets.Where(target => target.IsInspectable);

        return inspectableTargets.FirstOrDefault(target =>
        {
            if (target.WindowBounds is null)
            {
                return false;
            }

            return target.WindowBounds.Value.Contains(point);
        }) ?? inspectableTargets.FirstOrDefault();
    }
}
