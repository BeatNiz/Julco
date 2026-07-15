namespace Julco.Capture;

public interface IScreenSelectionService
{
    Task<SelectionRequest?> CaptureSelectionAsync(CancellationToken cancellationToken);
}
