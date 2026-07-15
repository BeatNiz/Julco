using System.Windows;
using Julco.Core.Clipboard;

namespace Julco.UI;

public sealed class SystemClipboardService : IClipboardService
{
    public Task CopyAsync(ClipboardContent content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        System.Windows.Clipboard.SetText(content.Text);
        return Task.CompletedTask;
    }
}
