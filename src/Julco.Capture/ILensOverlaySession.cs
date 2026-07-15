using Julco.Core.Geometry;

namespace Julco.Capture;

public interface ILensOverlaySession : IAsyncDisposable
{
    LensFrameState State { get; }

    event EventHandler<LensFrameChangedEventArgs>? Changed;

    Task MoveAsync(ScreenPoint topLeft, CancellationToken cancellationToken);

    Task ResizeAsync(ScreenRect bounds, CancellationToken cancellationToken);

    Task SetZoomAsync(double zoom, CancellationToken cancellationToken);

    Task PinAsync(CancellationToken cancellationToken);

    Task ReleaseAsync(CancellationToken cancellationToken);
}
