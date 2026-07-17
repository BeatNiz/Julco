using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Julco.Capture;
using Julco.Core.Geometry;
using Forms = System.Windows.Forms;

namespace Julco.UI;

public partial class LensWindow : Window
{
    private const double HeaderMinimumWidth = 240;
    private const double MinimumCaptureWidth = 34;
    private const double MinimumCaptureHeight = 34;
    private bool _isPinned;
    private bool _isResizing;
    private System.Windows.Point _resizeStartPoint;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    public LensWindow()
    {
        InitializeComponent();
        Left = 160;
        Top = 140;
        UpdateWindowSize();
        UpdateState();
    }

    public event EventHandler<LensFrameChangedEventArgs>? LensChanged;

    public event EventHandler<LensFrameState>? InspectCenterRequested;

    public event EventHandler<LensFrameState>? CaptureRequested;

    public LensFrameState State { get; private set; } = LensFrameState.FromBounds(
        new ScreenRect(160, 140, 420, 260));

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button)
        {
            return;
        }

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
        ResizeMode = ResizeMode.NoResize;
        UpdateState();
    }

    public void SetPinned(bool isPinned)
    {
        _isPinned = isPinned;
        ResizeMode = ResizeMode.NoResize;
        UpdateState();
    }

    public void InspectCenter()
    {
        UpdateState();
        RequestCenterInspection();
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateState();
        CaptureRequested?.Invoke(this, State);
    }

    private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isPinned)
        {
            return;
        }

        _isResizing = true;
        _resizeStartPoint = e.GetPosition(this);
        _resizeStartWidth = CaptureFrame.Width;
        _resizeStartHeight = CaptureFrame.Height;
        ResizeHandle.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isResizing = false;
        ResizeHandle.ReleaseMouseCapture();
        UpdateState();
        e.Handled = true;
    }

    private void ResizeHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isResizing || _isPinned)
        {
            return;
        }

        var currentPoint = e.GetPosition(this);
        var width = Math.Max(MinimumCaptureWidth, _resizeStartWidth + currentPoint.X - _resizeStartPoint.X);
        var height = Math.Max(MinimumCaptureHeight, _resizeStartHeight + currentPoint.Y - _resizeStartPoint.Y);
        CaptureFrame.Width = width;
        CaptureFrame.Height = height;
        UpdateWindowSize();
        UpdateState();
    }

    private void UpdateState()
    {
        var bounds = GetCaptureBounds();
        State = LensFrameState.FromBounds(bounds)
            with
            {
                IsPinned = _isPinned
            };

        PositionTextBlock.Text =
            $"Center {State.CenterPoint.X:0},{State.CenterPoint.Y:0}  |  {State.Bounds.Width:0}x{State.Bounds.Height:0}";
        UpdateHeaderPlacement(bounds);

        LensChanged?.Invoke(
            this,
            new LensFrameChangedEventArgs(
                State,
                _isPinned ? LensFrameChangeKind.Pinned : LensFrameChangeKind.Moved));

    }

    private void UpdateHeaderPlacement(ScreenRect bounds)
    {
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(
            (int)Math.Round(bounds.X + bounds.Width / 2),
            (int)Math.Round(bounds.Y + bounds.Height / 2)));
        var area = screen.WorkingArea;
        var shouldMoveHeaderDown = bounds.Y <= area.Top + 32;

        HeaderBorder.BorderThickness = shouldMoveHeaderDown
            ? new Thickness(0, 1, 0, 0)
            : new Thickness(0, 0, 0, 1);
        HeaderBorder.Width = Math.Max(HeaderMinimumWidth, CaptureFrame.Width);
        Canvas.SetTop(HeaderBorder, shouldMoveHeaderDown
            ? Math.Max(0, CaptureFrame.Height - HeaderBorder.Height)
            : 0);
        GuideGrid.Margin = shouldMoveHeaderDown
            ? new Thickness(2, 2, 2, 26)
            : new Thickness(2, 26, 2, 2);
    }

    private void UpdateWindowSize()
    {
        Width = Math.Max(HeaderMinimumWidth, CaptureFrame.Width);
        Height = Math.Max(MinimumCaptureHeight, CaptureFrame.Height);
        RootCanvas.Width = Width;
        RootCanvas.Height = Height;
        HeaderBorder.Width = Width;
    }

    private ScreenRect GetCaptureBounds()
    {
        try
        {
            var topLeft = CaptureFrame.PointToScreen(new System.Windows.Point(0, 0));
            var bottomRight = CaptureFrame.PointToScreen(new System.Windows.Point(CaptureFrame.Width, CaptureFrame.Height));
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
                CaptureFrame.Width,
                CaptureFrame.Height);
        }
    }

    private void RequestCenterInspection()
    {
        InspectCenterRequested?.Invoke(this, State);
    }
}
