namespace Julco.Capture;

public enum LensFrameChangeKind
{
    Created = 0,
    Moved,
    Resized,
    ZoomChanged,
    Pinned,
    Released,
    Closed
}
