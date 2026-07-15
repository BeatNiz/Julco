namespace Julco.Core.Geometry;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool Contains(ScreenPoint point)
    {
        return !IsEmpty
            && point.X >= X
            && point.Y >= Y
            && point.X <= Right
            && point.Y <= Bottom;
    }
}
