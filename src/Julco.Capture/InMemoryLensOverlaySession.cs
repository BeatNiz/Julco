using Julco.Core.Geometry;

namespace Julco.Capture;

public sealed class InMemoryLensOverlaySession : ILensOverlaySession
{
    public InMemoryLensOverlaySession(ScreenRect initialBounds)
    {
        State = LensFrameState.FromBounds(initialBounds);
    }

    public LensFrameState State { get; private set; }

    public event EventHandler<LensFrameChangedEventArgs>? Changed;

    public Task MoveAsync(ScreenPoint topLeft, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = State with
        {
            Bounds = new ScreenRect(topLeft.X, topLeft.Y, State.Bounds.Width, State.Bounds.Height),
            CenterPoint = new ScreenPoint(topLeft.X + State.Bounds.Width / 2, topLeft.Y + State.Bounds.Height / 2),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Notify(LensFrameChangeKind.Moved);
        return Task.CompletedTask;
    }

    public Task ResizeAsync(ScreenRect bounds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = LensFrameState.FromBounds(bounds, State.Zoom) with
        {
            IsPinned = State.IsPinned
        };
        Notify(LensFrameChangeKind.Resized);
        return Task.CompletedTask;
    }

    public Task SetZoomAsync(double zoom, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom), "Lens zoom must be greater than zero.");
        }

        State = State with
        {
            Zoom = zoom,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Notify(LensFrameChangeKind.ZoomChanged);
        return Task.CompletedTask;
    }

    public Task PinAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = State with
        {
            IsPinned = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Notify(LensFrameChangeKind.Pinned);
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = State with
        {
            IsPinned = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Notify(LensFrameChangeKind.Released);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Notify(LensFrameChangeKind.Closed);
        return ValueTask.CompletedTask;
    }

    private void Notify(LensFrameChangeKind changeKind)
    {
        Changed?.Invoke(this, new LensFrameChangedEventArgs(State, changeKind));
    }
}
