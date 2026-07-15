using Julco.Core.Geometry;

namespace Julco.Capture;

public sealed class InMemoryLensOverlayService : ILensOverlayService
{
    public Task<ILensOverlaySession> StartAsync(
        ScreenRect initialBounds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (initialBounds.IsEmpty)
        {
            throw new ArgumentException("Lens bounds cannot be empty.", nameof(initialBounds));
        }

        return Task.FromResult<ILensOverlaySession>(new InMemoryLensOverlaySession(initialBounds));
    }
}
