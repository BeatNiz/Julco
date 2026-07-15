using Julco.Core.Geometry;
using Xunit;

namespace Julco.Core.Tests;

public sealed class CoordinateMappingTests
{
    [Fact]
    public void ToViewportPointSubtractsWindowAndViewportOffsets()
    {
        var mapping = new CoordinateMapping(
            WindowBounds: new ScreenRect(100, 200, 1200, 800),
            ViewportBounds: new ViewportRect(8, 80, 1184, 700),
            DevicePixelRatio: 1,
            BrowserZoom: 1,
            WindowsScale: 1);

        var point = mapping.ToViewportPoint(new ScreenPoint(158, 330));

        Assert.Equal(50, point.X);
        Assert.Equal(50, point.Y);
    }
}
