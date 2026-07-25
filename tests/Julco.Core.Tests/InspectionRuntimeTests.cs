using Julco.Cdp;
using Xunit;

namespace Julco.Core.Tests;

public sealed class InspectionRuntimeTests
{
    [Fact]
    public void RuntimeExpressionsEmbedSharedInspectionApi()
    {
        var expression = InspectionRuntime.BuildSelectorExpression("main img");

        Assert.Contains("__julcoInspectionRuntime", expression);
        Assert.Contains("inspectSelector", expression);
        Assert.Contains("main img", expression);
    }

    [Fact]
    public void PayloadReaderMapsSharedRuntimeContract()
    {
        var payload = """
            {
              "found": true,
              "selector": "#hero",
              "tagName": "IMG",
              "attributes": {
                "src": "https://example.com/hero.webp",
                "alt": "Hero"
              },
              "outerHtml": "<img id=\"hero\" src=\"hero.webp\" alt=\"Hero\">",
              "computedStyle": {
                "display": "block",
                "object-fit": "cover"
              },
              "matchedCssRules": [".hero img", "#hero"],
              "images": [
                {
                  "url": "https://example.com/hero.webp",
                  "kind": "img",
                  "format": "webp",
                  "alt": "Hero",
                  "width": 320,
                  "height": 180,
                  "naturalWidth": 640,
                  "naturalHeight": 360,
                  "displayedWidth": 320,
                  "displayedHeight": 180,
                  "isAnimated": false
                }
              ]
            }
            """;

        var result = SelectorInspectionPayloadReader.Read(payload, new[] { "console:ok" }, "missing");

        Assert.Equal("#hero", result.Selector);
        Assert.Equal("IMG", result.TagName);
        Assert.Equal("Hero", result.Attributes["alt"]);
        Assert.Equal("cover", result.ComputedStyle["object-fit"]);
        Assert.Equal(new[] { ".hero img", "#hero" }, result.MatchedCssRules);
        Assert.Equal("console:ok", result.ConsoleMessages.Single());
        Assert.Collection(
            result.Images,
            image =>
            {
                Assert.Equal("https://example.com/hero.webp", image.Url);
                Assert.Equal("webp", image.Format);
                Assert.Equal(640, image.NaturalWidth);
                Assert.Equal(320, image.DisplayedWidth);
            });
    }

    [Fact]
    public void PayloadReaderUsesRuntimeMissingElementMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelectorInspectionPayloadReader.Read(
                """{"found":false,"message":"No element at calculated viewport point."}""",
                Array.Empty<string>(),
                "fallback"));

        Assert.Equal("No element at calculated viewport point.", exception.Message);
    }
}
