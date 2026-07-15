namespace Julco.Capture;

public sealed class LensFrameChangedEventArgs : EventArgs
{
    public LensFrameChangedEventArgs(LensFrameState state, LensFrameChangeKind changeKind)
    {
        State = state;
        ChangeKind = changeKind;
    }

    public LensFrameState State { get; }

    public LensFrameChangeKind ChangeKind { get; }
}
