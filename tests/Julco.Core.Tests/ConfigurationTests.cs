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
        Assert.False(settings.Privacy.IncludeScreenshotsInSafeExports);
        Assert.Equal("Win+Shift+D", settings.Capture.GlobalShortcut);
        Assert.True(settings.Keyboard.EnableGlobalShortcuts);
        Assert.True(settings.Keyboard.EnableLocalShortcuts);
        Assert.Equal("Ctrl+Alt+Shift+L", settings.Keyboard.GlobalShortcuts[KeyboardShortcutSettings.ToggleLens]);
        Assert.Equal("Ctrl+Shift+C", settings.Keyboard.LocalShortcuts[KeyboardShortcutSettings.CaptureLens]);
        Assert.Equal(ExportFormat.Json, settings.Export.DefaultFormat);
        Assert.False(settings.IssueTrackers.EnableGitHub);
        Assert.False(settings.IssueTrackers.EnableJira);
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
                "RedactSelectedText": false,
                "IncludeScreenshotsInSafeExports": false
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
            Assert.NotNull(settings.IssueTrackers);
            Assert.Equal("Ctrl+Alt+Shift+D", settings.Keyboard.GlobalShortcuts[KeyboardShortcutSettings.OpenDom]);
            Assert.Equal("Bug", settings.IssueTrackers.JiraIssueType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task JsonSettingsStoreProtectsIssueTrackerTokensOnSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"julco-settings-{Guid.NewGuid():N}.json");
        var settings = AppSettings.Default with
        {
            IssueTrackers = new IssueTrackerSettings(
                EnableGitHub: true,
                GitHubOwner: "BeatNiz",
                GitHubRepository: "Julco",
                GitHubToken: "github-token",
                GitHubLabels: "julco",
                GitHubAssignees: "",
                GitHubMilestone: "",
                EnableJira: true,
                JiraBaseUrl: "https://example.atlassian.net",
                JiraProjectKey: "QA",
                JiraIssueType: "Bug",
                JiraPriority: "High",
                JiraEmail: "user@example.com",
                JiraApiToken: "jira-token")
        };

        try
        {
            await new JsonSettingsStore(path).SaveAsync(settings, CancellationToken.None);
            var savedJson = await File.ReadAllTextAsync(path);

            if (OperatingSystem.IsWindows())
            {
                Assert.DoesNotContain("github-token", savedJson);
                Assert.DoesNotContain("jira-token", savedJson);
                Assert.Contains("dpapi:", savedJson);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void IssueTrackerSettingsNormalizeAndDetectConfiguration()
    {
        var settings = new IssueTrackerSettings(
            EnableGitHub: true,
            GitHubOwner: " BeatNiz ",
            GitHubRepository: " Julco ",
            GitHubToken: " ghp_example ",
            GitHubLabels: " bug, julco, bug ",
            GitHubAssignees: " alice, bob, alice ",
            GitHubMilestone: " 7 ",
            EnableJira: true,
            JiraBaseUrl: " https://example.atlassian.net/ ",
            JiraProjectKey: " qa ",
            JiraIssueType: "",
            JiraPriority: " High ",
            JiraEmail: " user@example.com ",
            JiraApiToken: " token ").Normalized();

        Assert.True(settings.IsGitHubConfigured);
        Assert.True(settings.IsJiraConfigured);
        Assert.Equal("BeatNiz", settings.GitHubOwner);
        Assert.Equal("Julco", settings.GitHubRepository);
        Assert.Equal("https://example.atlassian.net", settings.JiraBaseUrl);
        Assert.Equal("QA", settings.JiraProjectKey);
        Assert.Equal("Bug", settings.JiraIssueType);
        Assert.Equal("High", settings.JiraPriority);
        Assert.Equal(7, settings.GitHubMilestoneNumber);
        Assert.Equal(new[] { "bug", "julco" }, settings.GitHubLabelList);
        Assert.Equal(new[] { "alice", "bob" }, settings.GitHubAssigneeList);
    }

    [Fact]
    public void IssueTrackerSettingsProtectSecretsWithoutChangingResolvedTokens()
    {
        var settings = new IssueTrackerSettings(
            EnableGitHub: true,
            GitHubOwner: "BeatNiz",
            GitHubRepository: "Julco",
            GitHubToken: "github-token",
            GitHubLabels: "julco",
            GitHubAssignees: "",
            GitHubMilestone: "",
            EnableJira: true,
            JiraBaseUrl: "https://example.atlassian.net",
            JiraProjectKey: "QA",
            JiraIssueType: "Bug",
            JiraPriority: "High",
            JiraEmail: "user@example.com",
            JiraApiToken: "jira-token").WithProtectedSecrets();

        if (OperatingSystem.IsWindows())
        {
            Assert.True(SecretProtector.IsProtected(settings.GitHubToken));
            Assert.True(SecretProtector.IsProtected(settings.JiraApiToken));
        }

        Assert.Equal("github-token", settings.ResolveGitHubToken());
        Assert.Equal("jira-token", settings.ResolveJiraApiToken());
    }
}
