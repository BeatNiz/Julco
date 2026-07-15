using Julco.Capture;
using Julco.Core.Geometry;
using Xunit;

namespace Julco.Core.Tests;

public sealed class LensOverlayTests
{
    [Fact]
    public async Task MoveUpdatesBoundsAndCenterPoint()
    {
        var session = new InMemoryLensOverlaySession(new ScreenRect(10, 10, 100, 50));

        await session.MoveAsync(new ScreenPoint(30, 40), CancellationToken.None);

        Assert.Equal(new ScreenRect(30, 40, 100, 50), session.State.Bounds);
        Assert.Equal(new ScreenPoint(80, 65), session.State.CenterPoint);
    }

    [Fact]
    public async Task ResizeKeepsCurrentZoom()
    {
        var session = new InMemoryLensOverlaySession(new ScreenRect(10, 10, 100, 50));
        await session.SetZoomAsync(2, CancellationToken.None);

        await session.ResizeAsync(new ScreenRect(0, 0, 200, 100), CancellationToken.None);

        Assert.Equal(2, session.State.Zoom);
        Assert.Equal(new ScreenPoint(100, 50), session.State.CenterPoint);
    }

    [Fact]
    public async Task ServiceRejectsEmptyInitialBounds()
    {
        var service = new InMemoryLensOverlayService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(new ScreenRect(0, 0, 0, 100), CancellationToken.None));
    }
}
