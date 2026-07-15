using Julco.Core.Geometry;

namespace Julco.Capture;

public sealed record LensFrameState(
    ScreenRect Bounds,
    ScreenPoint CenterPoint,
    double Zoom,
    bool IsPinned,
    DateTimeOffset UpdatedAt)
{
    public static LensFrameState FromBounds(ScreenRect bounds, double zoom = 1)
    {
        return new LensFrameState(
            bounds,
            new ScreenPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
            zoom,
            IsPinned: false,
            DateTimeOffset.UtcNow);
    }
}
