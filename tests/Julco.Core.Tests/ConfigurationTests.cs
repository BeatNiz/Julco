using Julco.Configuration;
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
        Assert.Equal(UsageProfile.QA, settings.Ui.Profile);
        Assert.True(settings.Privacy.RedactOnExport);
        Assert.Equal("Win+Shift+D", settings.Capture.GlobalShortcut);
        Assert.True(settings.Keyboard.EnableGlobalShortcuts);
        Assert.True(settings.Keyboard.EnableLocalShortcuts);
        Assert.Equal("Ctrl+Alt+Shift+L", settings.Keyboard.GlobalShortcuts[KeyboardShortcutSettings.ToggleLens]);
        Assert.Equal("Ctrl+Shift+C", settings.Keyboard.LocalShortcuts[KeyboardShortcutSettings.CaptureLens]);
        Assert.Equal(ExportFormat.Json, settings.Export.DefaultFormat);
        Assert.True(settings.History.MaxEntries > 0);
    }

    [Fact]
    public void KeyboardShortcutSettingsNormalizeMissingActionsAndKeepBlankOverrides()
    {
        var settings = new KeyboardShortcutSettings(
            EnableGlobalShortcuts: true,
            EnableLocalShortcuts: false,
            GlobalShortcuts: new Dictionary<string, string>
            {
                [KeyboardShortcutSettings.ToggleLens] = ""
            },
            LocalShortcuts: new Dictionary<string, string>())
            .Normalized();

        Assert.Empty(settings.GlobalShortcuts[KeyboardShortcutSettings.ToggleLens]);
        Assert.Equal("Ctrl+Alt+Shift+C", settings.GlobalShortcuts[KeyboardShortcutSettings.CaptureLens]);
        Assert.Equal("Ctrl+Shift+I", settings.LocalShortcuts[KeyboardShortcutSettings.OpenImages]);
        Assert.False(settings.EnableLocalShortcuts);
    }

    [Fact]
    public async Task JsonSettingsStoreNormalizesLegacySettingsWithoutKeyboardSection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"julco-settings-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "Theme": 2,
              "Language": "en-US",
              "Capture": {
                "GlobalShortcut": "Win+Shift+D",
                "ScreenshotDirectory": "",
                "FileNamePattern": "julco-{date}-{time}-{tag}"
              },
              "Export": {
                "DefaultFormat": 0,
                "IncludeWarnings": true,
                "IncludeAccessibility": true
              },
              "History": {
                "MaxEntries": 50
              },
              "Privacy": {
                "RedactOnExport": true,
                "RedactEmails": true,
                "RedactTokens": true,
                "RedactCookies": true,
                "RedactPrivateUrls": true,
                "RedactSelectedText": false
              },
              "Ui": {
                "CdpPort": 9222,
                "LensInspectionDelayMs": 220,
                "KeepResultWindowsTopmost": true,
                "Profile": 0
              }
            }
            """);

        try
        {
            var settings = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

            Assert.NotNull(settings.Keyboard);
            Assert.Equal("Ctrl+Alt+Shift+D", settings.Keyboard.GlobalShortcuts[KeyboardShortcutSettings.OpenDom]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
