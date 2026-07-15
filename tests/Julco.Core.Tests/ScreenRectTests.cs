using Julco.Core.Geometry;
using Xunit;

namespace Julco.Core.Tests;

public sealed class ScreenRectTests
{
    [Fact]
    public void IsEmptyReturnsTrueWhenWidthIsZero()
    {
        var rect = new ScreenRect(10, 20, 0, 100);

        Assert.True(rect.IsEmpty);
    }

    [Fact]
    public void ContainsReturnsTrueWhenPointIsInside()
    {
        var rect = new ScreenRect(10, 20, 100, 50);

        Assert.True(rect.Contains(new ScreenPoint(40, 40)));
    }

    [Fact]
    public void ContainsReturnsFalseWhenRectIsEmpty()
    {
        var rect = new ScreenRect(10, 20, 0, 50);

        Assert.False(rect.Contains(new ScreenPoint(10, 20)));
    }
}
