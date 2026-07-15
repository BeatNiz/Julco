using Julco.Core.Geometry;

namespace Julco.Capture;

public interface ILensOverlayService
{
    Task<ILensOverlaySession> StartAsync(
        ScreenRect initialBounds,
        CancellationToken cancellationToken);
}
