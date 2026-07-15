using Julco.Browser;
using Julco.Core.Geometry;
using Julco.Core.Inspection;
using Xunit;

namespace Julco.Core.Tests;

public sealed class BrowserManagerTests
{
    [Fact]
    public async Task InspectAtPointPrefersTargetContainingPoint()
    {
        var expectedTarget = new BrowserTarget(
            TargetId: "target-2",
            BrowserKind: BrowserKind.Chrome,
            Title: "Expected",
            Url: new Uri("https://example.com"),
            ProcessId: 100,
            WindowBounds: new ScreenRect(200, 200, 500, 500),
            IsInspectable: true);

        var adapter = new FakeBrowserAdapter(new[]
        {
            expectedTarget with
            {
                TargetId = "target-1",
                Title = "Other",
                WindowBounds = new ScreenRect(0, 0, 100, 100)
            },
            expectedTarget
        });

        var manager = new BrowserManager(new[] { adapter });

        var result = await manager.InspectAtPointAsync(
            BrowserKind.Chrome,
            new ScreenPoint(300, 300),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("target-2", result.Target.TargetId);
    }

    private sealed class FakeBrowserAdapter : IBrowserAdapter
    {
        private readonly IReadOnlyList<BrowserTarget> _targets;
        private BrowserTarget? _connectedTarget;

        public FakeBrowserAdapter(IReadOnlyList<BrowserTarget> targets)
        {
            _targets = targets;
        }

        public BrowserKind BrowserKind => BrowserKind.Chrome;

        public BrowserAdapterCapabilities Capabilities { get; } = new(
            SupportsPointInspection: true,
            SupportsRegionInspection: false,
            SupportsComputedStyle: true,
            SupportsMatchedCss: true,
            SupportsAccessibilityTree: false,
            SupportsRuntimeConsole: false,
            SupportsElementCapture: false,
            SupportsNodeHighlight: false);

        public Task<IReadOnlyList<BrowserTarget>> DiscoverTargetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_targets);
        }

        public Task ConnectAsync(BrowserTarget target, CancellationToken cancellationToken)
        {
            _connectedTarget = target;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            _connectedTarget = null;
            return Task.CompletedTask;
        }

        public Task<InspectionResult?> InspectAsync(
            InspectionRequest request,
            CancellationToken cancellationToken)
        {
            var target = _connectedTarget ?? request.Target;
            var result = new InspectionResult(
                target,
                new InspectedElement(
                    new ElementHandle("node-1", null, null),
                    "button",
                    null,
                    Array.Empty<string>(),
                    new Dictionary<string, string>(),
                    new ScreenRect(0, 0, 10, 10),
                    new ScreenRect(0, 0, 10, 10),
                    "button",
                    "button",
                    "/html/body/button",
                    Depth: 2,
                    ChildCount: 0,
                    SiblingCount: 0),
                new DomSnapshot(null, null, null, Array.Empty<string>(), false, false, false, false),
                new CssSnapshot(
                    Array.Empty<CssDeclaration>(),
                    Array.Empty<CssRule>(),
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>()),
                Accessibility: null,
                RuntimeConsole: null,
                CapturedAt: DateTimeOffset.UtcNow,
                Warnings: Array.Empty<InspectionWarning>());

            return Task.FromResult<InspectionResult?>(result);
        }

        public Task<IReadOnlyList<InspectedElement>> FindElementsInRegionAsync(
            ScreenRect region,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<InspectedElement>>(Array.Empty<InspectedElement>());
        }

        public Task HighlightNodeAsync(ElementHandle handle, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
