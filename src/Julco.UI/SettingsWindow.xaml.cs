using System.Windows;
using System.Windows.Media;
using Julco.Core.Configuration;
using Forms = System.Windows.Forms;

namespace Julco.UI;

public partial class SettingsWindow : Window
{
    private readonly List<ShortcutEditorRow> _shortcutRows = new();
    private readonly IssueTrackerClient _issueTrackerClient = new();

    public SettingsWindow(AppSettings settings, string resolvedCaptureDirectory)
    {
        InitializeComponent();
        Settings = settings with
        {
            Keyboard = (settings.Keyboard ?? KeyboardShortcutSettings.Default).Normalized(),
            IssueTrackers = (settings.IssueTrackers ?? IssueTrackerSettings.Default).Normalized()
        };
        CdpPortTextBox.Text = settings.Ui.CdpPort.ToString();
        CaptureDirectoryTextBox.Text = string.IsNullOrWhiteSpace(settings.Capture.ScreenshotDirectory)
            ? resolvedCaptureDirectory
            : settings.Capture.ScreenshotDirectory;
        FilePatternTextBox.Text = settings.Capture.FileNamePattern;
        HistoryMaxTextBox.Text = settings.History.MaxEntries.ToString();
        LensDelayTextBox.Text = settings.Ui.LensInspectionDelayMs.ToString();
        ThemeComboBox.ItemsSource = Enum.GetValues<ThemeMode>();
        ThemeComboBox.SelectedItem = settings.Theme;
        TopmostCheckBox.IsChecked = settings.Ui.KeepResultWindowsTopmost;
        EnableGlobalShortcutsCheckBox.IsChecked = Settings.Keyboard.EnableGlobalShortcuts;
        EnableLocalShortcutsCheckBox.IsChecked = Settings.Keyboard.EnableLocalShortcuts;
        LoadShortcutRows(Settings.Keyboard);
        RedactOnExportCheckBox.IsChecked = settings.Privacy.RedactOnExport;
        RedactEmailsCheckBox.IsChecked = settings.Privacy.RedactEmails;
        RedactTokensCheckBox.IsChecked = settings.Privacy.RedactTokens;
        RedactCookiesCheckBox.IsChecked = settings.Privacy.RedactCookies;
        RedactPrivateUrlsCheckBox.IsChecked = settings.Privacy.RedactPrivateUrls;
        RedactSelectedTextCheckBox.IsChecked = settings.Privacy.RedactSelectedText;
        IncludeScreenshotsInSafeExportsCheckBox.IsChecked = settings.Privacy.IncludeScreenshotsInSafeExports;
        EnableGitHubCheckBox.IsChecked = Settings.IssueTrackers.EnableGitHub;
        GitHubOwnerTextBox.Text = Settings.IssueTrackers.GitHubOwner;
        GitHubRepositoryTextBox.Text = Settings.IssueTrackers.GitHubRepository;
        GitHubTokenTextBox.Text = Settings.IssueTrackers.GitHubToken;
        GitHubLabelsTextBox.Text = Settings.IssueTrackers.GitHubLabels;
        EnableJiraCheckBox.IsChecked = Settings.IssueTrackers.EnableJira;
        JiraBaseUrlTextBox.Text = Settings.IssueTrackers.JiraBaseUrl;
        JiraProjectKeyTextBox.Text = Settings.IssueTrackers.JiraProjectKey;
        JiraIssueTypeTextBox.Text = Settings.IssueTrackers.JiraIssueType;
        JiraEmailTextBox.Text = Settings.IssueTrackers.JiraEmail;
        JiraApiTokenTextBox.Text = Settings.IssueTrackers.JiraApiToken;
        WireIntegrationStatusEvents();
        UpdateIntegrationIndicators();
        ApplyTheme(settings.Theme);
    }

    public AppSettings Settings { get; private set; }

    private void LoadShortcutRows(KeyboardShortcutSettings keyboard)
    {
        _shortcutRows.Clear();
        foreach (var action in HotkeyActionCatalog.All)
        {
            keyboard.GlobalShortcuts.TryGetValue(action.Id, out var globalShortcut);
            keyboard.LocalShortcuts.TryGetValue(action.Id, out var localShortcut);
            _shortcutRows.Add(new ShortcutEditorRow
            {
                ActionId = action.Id,
                ActionName = action.Name,
                Description = action.Description,
                GlobalShortcut = globalShortcut ?? string.Empty,
                LocalShortcut = localShortcut ?? string.Empty
            });
        }

        ShortcutsDataGrid.ItemsSource = null;
        ShortcutsDataGrid.ItemsSource = _shortcutRows;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose the Julco capture folder",
            SelectedPath = CaptureDirectoryTextBox.Text
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            CaptureDirectoryTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(CdpPortTextBox.Text, out var port) || port <= 0 || port > 65535)
        {
            ShowValidation("Remote port must be between 1 and 65535.");
            return;
        }

        if (!int.TryParse(HistoryMaxTextBox.Text, out var historyMax) || historyMax < 1 || historyMax > 500)
        {
            ShowValidation("History max must be between 1 and 500.");
            return;
        }

        if (!int.TryParse(LensDelayTextBox.Text, out var lensDelay) || lensDelay < 150 || lensDelay > 5000)
        {
            ShowValidation("Lens delay must be between 150 and 5000 ms.");
            return;
        }

        var captureDirectory = CaptureDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(captureDirectory))
        {
            ShowValidation("Capture folder cannot be empty.");
            return;
        }

        var pattern = FilePatternTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            ShowValidation("File pattern cannot be empty.");
            return;
        }

        if (!TryBuildKeyboardSettings(out var keyboardSettings))
        {
            return;
        }

        var issueTrackers = BuildIssueTrackerSettings();
        if (!ValidateIssueTrackerSettings(issueTrackers))
        {
            return;
        }

        Settings = Settings with
        {
            Theme = ThemeComboBox.SelectedItem is ThemeMode theme
                ? theme
                : ThemeMode.Dark,
            Capture = Settings.Capture with
            {
                ScreenshotDirectory = captureDirectory,
                FileNamePattern = pattern
            },
            History = Settings.History with
            {
                MaxEntries = historyMax
            },
            Privacy = Settings.Privacy with
            {
                RedactOnExport = RedactOnExportCheckBox.IsChecked == true,
                RedactEmails = RedactEmailsCheckBox.IsChecked == true,
                RedactTokens = RedactTokensCheckBox.IsChecked == true,
                RedactCookies = RedactCookiesCheckBox.IsChecked == true,
                RedactPrivateUrls = RedactPrivateUrlsCheckBox.IsChecked == true,
                RedactSelectedText = RedactSelectedTextCheckBox.IsChecked == true,
                IncludeScreenshotsInSafeExports = IncludeScreenshotsInSafeExportsCheckBox.IsChecked == true
            },
            Keyboard = keyboardSettings,
            IssueTrackers = issueTrackers,
            Ui = Settings.Ui with
            {
                CdpPort = port,
                LensInspectionDelayMs = lensDelay,
                KeepResultWindowsTopmost = TopmostCheckBox.IsChecked == true
            }
        };

        DialogResult = true;
        Close();
    }

    private bool TryBuildKeyboardSettings(out KeyboardShortcutSettings keyboardSettings)
    {
        var globalShortcuts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var localShortcuts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        keyboardSettings = KeyboardShortcutSettings.Default;

        foreach (var row in _shortcutRows)
        {
            var globalShortcut = HotkeyTextParser.Normalize(row.GlobalShortcut);
            var localShortcut = HotkeyTextParser.Normalize(row.LocalShortcut);
            if (!ValidateShortcut(row.ActionName, "global", globalShortcut)
                || !ValidateShortcut(row.ActionName, "local", localShortcut))
            {
                return false;
            }

            globalShortcuts[row.ActionId] = globalShortcut;
            localShortcuts[row.ActionId] = localShortcut;
        }

        if (!ValidateNoDuplicates("global", globalShortcuts)
            || !ValidateNoDuplicates("local", localShortcuts))
        {
            return false;
        }

        keyboardSettings = new KeyboardShortcutSettings(
            EnableGlobalShortcutsCheckBox.IsChecked == true,
            EnableLocalShortcutsCheckBox.IsChecked == true,
            globalShortcuts,
            localShortcuts).Normalized();
        return true;
    }

    private IssueTrackerSettings BuildIssueTrackerSettings()
    {
        return new IssueTrackerSettings(
            EnableGitHubCheckBox.IsChecked == true,
            GitHubOwnerTextBox.Text,
            GitHubRepositoryTextBox.Text,
            GitHubTokenTextBox.Text,
            GitHubLabelsTextBox.Text,
            EnableJiraCheckBox.IsChecked == true,
            JiraBaseUrlTextBox.Text,
            JiraProjectKeyTextBox.Text,
            JiraIssueTypeTextBox.Text,
            JiraEmailTextBox.Text,
            JiraApiTokenTextBox.Text).Normalized();
    }

    private void WireIntegrationStatusEvents()
    {
        EnableGitHubCheckBox.Checked += IntegrationField_Changed;
        EnableGitHubCheckBox.Unchecked += IntegrationField_Changed;
        GitHubOwnerTextBox.TextChanged += IntegrationField_Changed;
        GitHubRepositoryTextBox.TextChanged += IntegrationField_Changed;
        GitHubTokenTextBox.TextChanged += IntegrationField_Changed;
        GitHubLabelsTextBox.TextChanged += IntegrationField_Changed;
        EnableJiraCheckBox.Checked += IntegrationField_Changed;
        EnableJiraCheckBox.Unchecked += IntegrationField_Changed;
        JiraBaseUrlTextBox.TextChanged += IntegrationField_Changed;
        JiraProjectKeyTextBox.TextChanged += IntegrationField_Changed;
        JiraIssueTypeTextBox.TextChanged += IntegrationField_Changed;
        JiraEmailTextBox.TextChanged += IntegrationField_Changed;
        JiraApiTokenTextBox.TextChanged += IntegrationField_Changed;
    }

    private void IntegrationField_Changed(object sender, RoutedEventArgs e)
    {
        UpdateIntegrationIndicators();
    }

    private void UpdateIntegrationIndicators()
    {
        var settings = BuildIssueTrackerSettings();
        UpdateIntegrationIndicator(
            GitHubStatusTextBlock,
            TestGitHubButton,
            settings.EnableGitHub,
            settings.IsGitHubConfigured,
            string.IsNullOrWhiteSpace(settings.ResolveGitHubToken()) ? "Missing token" : "Missing setup",
            $"Ready: {settings.GitHubOwner}/{settings.GitHubRepository}");
        UpdateIntegrationIndicator(
            JiraStatusTextBlock,
            TestJiraButton,
            settings.EnableJira,
            settings.IsJiraConfigured,
            string.IsNullOrWhiteSpace(settings.ResolveJiraApiToken()) ? "Missing token" : "Missing setup",
            $"Ready: {settings.JiraProjectKey}");
    }

    private void UpdateIntegrationIndicator(
        System.Windows.Controls.TextBlock statusTextBlock,
        System.Windows.Controls.Button testButton,
        bool enabled,
        bool ready,
        string missingText,
        string readyText)
    {
        if (!enabled)
        {
            statusTextBlock.Text = "Disabled";
            statusTextBlock.Foreground = (System.Windows.Media.Brush)Resources["StatusDisabled"];
            testButton.IsEnabled = false;
            return;
        }

        if (!ready)
        {
            statusTextBlock.Text = missingText;
            statusTextBlock.Foreground = (System.Windows.Media.Brush)Resources["StatusWarning"];
            testButton.IsEnabled = false;
            return;
        }

        statusTextBlock.Text = readyText;
        statusTextBlock.Foreground = (System.Windows.Media.Brush)Resources["StatusReady"];
        testButton.IsEnabled = true;
    }

    private async void TestGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        await TestIntegrationAsync(
            TestGitHubButton,
            GitHubStatusTextBlock,
            settings => _issueTrackerClient.TestGitHubAsync(settings, CancellationToken.None));
    }

    private async void TestJiraButton_Click(object sender, RoutedEventArgs e)
    {
        await TestIntegrationAsync(
            TestJiraButton,
            JiraStatusTextBlock,
            settings => _issueTrackerClient.TestJiraAsync(settings, CancellationToken.None));
    }

    private async Task TestIntegrationAsync(
        System.Windows.Controls.Button button,
        System.Windows.Controls.TextBlock statusTextBlock,
        Func<IssueTrackerSettings, Task<IssueTrackerSubmissionResult>> test)
    {
        var settings = BuildIssueTrackerSettings();
        button.IsEnabled = false;
        statusTextBlock.Text = "Testing...";
        statusTextBlock.Foreground = (System.Windows.Media.Brush)Resources["StatusWarning"];
        try
        {
            var result = await test(settings);
            statusTextBlock.Text = result.Message;
            statusTextBlock.Foreground = (System.Windows.Media.Brush)Resources[result.Succeeded ? "StatusReady" : "StatusWarning"];
        }
        catch (Exception exception)
        {
            statusTextBlock.Text = $"Test failed: {exception.Message}";
            statusTextBlock.Foreground = (System.Windows.Media.Brush)Resources["StatusWarning"];
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private bool ValidateIssueTrackerSettings(IssueTrackerSettings settings)
    {
        if (settings.EnableGitHub
            && (string.IsNullOrWhiteSpace(settings.GitHubOwner)
                || string.IsNullOrWhiteSpace(settings.GitHubRepository)))
        {
            ShowValidation("GitHub needs owner and repository when enabled.");
            return false;
        }

        if (settings.EnableJira)
        {
            if (string.IsNullOrWhiteSpace(settings.JiraBaseUrl)
                || string.IsNullOrWhiteSpace(settings.JiraProjectKey)
                || string.IsNullOrWhiteSpace(settings.JiraEmail))
            {
                ShowValidation("Jira needs base URL, project key, and email when enabled.");
                return false;
            }

            if (!Uri.TryCreate(settings.JiraBaseUrl, UriKind.Absolute, out var jiraUri)
                || jiraUri.Scheme is not ("http" or "https"))
            {
                ShowValidation("Jira base URL must be a valid http or https URL.");
                return false;
            }
        }

        return true;
    }

    private bool ValidateShortcut(string actionName, string scope, string shortcut)
    {
        var parsed = HotkeyTextParser.Parse(shortcut);
        if (parsed.Error is null)
        {
            return true;
        }

        ShowValidation($"{actionName} has an invalid {scope} shortcut: {parsed.Error}");
        return false;
    }

    private bool ValidateNoDuplicates(string scope, IReadOnlyDictionary<string, string> shortcuts)
    {
        var duplicates = shortcuts
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is null)
        {
            return true;
        }

        var names = duplicates
            .Select(pair => _shortcutRows.First(row => row.ActionId == pair.Key).ActionName);
        ShowValidation($"The {scope} shortcut {duplicates.Key} is assigned to multiple actions: {string.Join(", ", names)}.");
        return false;
    }

    private void RestoreShortcutsButton_Click(object sender, RoutedEventArgs e)
    {
        EnableGlobalShortcutsCheckBox.IsChecked = KeyboardShortcutSettings.Default.EnableGlobalShortcuts;
        EnableLocalShortcutsCheckBox.IsChecked = KeyboardShortcutSettings.Default.EnableLocalShortcuts;
        LoadShortcutRows(KeyboardShortcutSettings.Default);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowValidation(string message)
    {
        System.Windows.MessageBox.Show(this, message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ApplyTheme(ThemeMode theme)
    {
        var light = theme == ThemeMode.Light
            || theme == ThemeMode.System
            && SystemParameters.WindowGlassColor.R
            + SystemParameters.WindowGlassColor.G
            + SystemParameters.WindowGlassColor.B > 382;

        SetBrush("PanelBackground", light ? "#F4F7FB" : "#101217");
        SetBrush("PrimaryText", light ? "#121826" : "#F4F6F8");
        SetBrush("InputBackground", light ? "#FFFFFF" : "#0F1218");
        SetBrush("ControlBackground", light ? "#EEF3F8" : "#202630");
        SetBrush("MutedText", light ? "#526173" : "#AAB2C0");
        SetBrush("BorderColor", light ? "#CFD8E3" : "#2A2F3A");
        SetBrush("StatusReady", light ? "#087A3D" : "#45D483");
        SetBrush("StatusWarning", light ? "#9A5B00" : "#FFCA5C");
        SetBrush("StatusDisabled", light ? "#68778A" : "#AAB2C0");

        Background = (System.Windows.Media.Brush)Resources["PanelBackground"];
        Foreground = (System.Windows.Media.Brush)Resources["PrimaryText"];
        UpdateIntegrationIndicators();
    }

    private void SetBrush(string key, string color)
    {
        if (Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
            return;
        }

        Resources[key] = new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    private sealed class ShortcutEditorRow
    {
        public string ActionId { get; init; } = string.Empty;

        public string ActionName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string GlobalShortcut { get; set; } = string.Empty;

        public string LocalShortcut { get; set; } = string.Empty;
    }
}
