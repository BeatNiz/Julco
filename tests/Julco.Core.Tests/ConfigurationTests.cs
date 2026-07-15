using Julco.Core.Configuration;
using Julco.Core.Exporting;
using Xunit;

namespace Julco.Core.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void DefaultSettingsUseMvpFriendlyValues()
    {
        var settings = AppSettings.Default;

        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.Equal("Win+Shift+D", settings.Capture.GlobalShortcut);
        Assert.Equal(ExportFormat.Json, settings.Export.DefaultFormat);
        Assert.True(settings.History.MaxEntries > 0);
    }
}
