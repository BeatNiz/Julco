using Julco.Core.Inspection;

namespace Julco.Cdp;

public sealed class EdgeCdpBrowserAdapter : ChromiumCdpBrowserAdapter
{
    public override BrowserKind BrowserKind => BrowserKind.Edge;
}
