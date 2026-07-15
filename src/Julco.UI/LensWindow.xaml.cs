using System.Windows;
using System.Windows.Input;
using Julco.Capture;
using Julco.Core.Geometry;

namespace Julco.UI;

public partial class LensWindow : Window
{
    private bool _isPinned;

    public LensWindow()
    {
        InitializeComponent();
        Left = 160;
        Top = 140;
        UpdateState();
    }

    public event EventHandler<LensFrameChangedEventArgs>? LensChanged;

    public event EventHandler<LensFrameState>? InspectCenterRequested;

    public LensFrameState State { get; private set; } = LensFrameState.FromBounds(
        new ScreenRect(160, 140, 420, 260));

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isPinned)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            TogglePin();
            return;
        }

        DragMove();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        UpdateState();
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_isPinned)
        {
            Topmost = true;
        }
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void TogglePin()
    {
        _isPinned = !_isPinned;
        ResizeMode = _isPinned ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        UpdateState();
    }

    public void SetPinned(bool isPinned)
    {
        _isPinned = isPinned;
        ResizeMode = _isPinned ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        UpdateState();
    }

    public void InspectCenter()
    {
        UpdateState();
        RequestCenterInspection();
    }

    private void UpdateState()
    {
        var bounds = GetScreenBounds();
        State = LensFrameState.FromBounds(bounds)
            with
            {
                IsPinned = _isPinned
            };

        PositionTextBlock.Text =
            $"Center {State.CenterPoint.X:0},{State.CenterPoint.Y:0}  |  {State.Bounds.Width:0}x{State.Bounds.Height:0}";

        LensChanged?.Invoke(
            this,
            new LensFrameChangedEventArgs(
                State,
                _isPinned ? LensFrameChangeKind.Pinned : LensFrameChangeKind.Moved));

    }

    private ScreenRect GetScreenBounds()
    {
        try
        {
            var topLeft = PointToScreen(new System.Windows.Point(0, 0));
            var bottomRight = PointToScreen(new System.Windows.Point(ActualWidth, ActualHeight));
            return new ScreenRect(
                topLeft.X,
                topLeft.Y,
                Math.Max(0, bottomRight.X - topLeft.X),
                Math.Max(0, bottomRight.Y - topLeft.Y));
        }
        catch (InvalidOperationException)
        {
            return new ScreenRect(
                Left,
                Top,
                ActualWidth > 0 ? ActualWidth : Width,
                ActualHeight > 0 ? ActualHeight : Height);
        }
    }

    private void RequestCenterInspection()
    {
        InspectCenterRequested?.Invoke(this, State);
    }
}
