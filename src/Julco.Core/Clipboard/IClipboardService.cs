namespace Julco.Core.Clipboard;

public interface IClipboardService
{
    Task CopyAsync(ClipboardContent content, CancellationToken cancellationToken);
}
