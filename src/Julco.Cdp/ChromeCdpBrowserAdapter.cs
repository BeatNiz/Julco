using Julco.Core.Inspection;

namespace Julco.Cdp;

public sealed class ChromeCdpBrowserAdapter : ChromiumCdpBrowserAdapter
{
    public override BrowserKind BrowserKind => BrowserKind.Chrome;
}
