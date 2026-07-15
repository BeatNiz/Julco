namespace Julco.Core.Geometry;

public sealed record CoordinateMapping(
    ScreenRect WindowBounds,
    ViewportRect ViewportBounds,
    double DevicePixelRatio,
    double BrowserZoom,
    double WindowsScale)
{
    public ViewportPoint ToViewportPoint(ScreenPoint screenPoint)
    {
        var viewportX = (screenPoint.X - WindowBounds.X - ViewportBounds.X) / WindowsScale;
        var viewportY = (screenPoint.Y - WindowBounds.Y - ViewportBounds.Y) / WindowsScale;

        return new ViewportPoint(viewportX, viewportY);
    }
}
