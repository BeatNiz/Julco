using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Julco.Configuration;
using Julco.Capture;
using Julco.Cdp;
using Julco.Core.Configuration;
using Forms = System.Windows.Forms;

namespace Julco.UI;

public partial class MainWindow : Window
{
    private readonly ChromiumBrowserLauncher _browserLauncher = new();
    private readonly CdpEndpointClient _endpointClient = new();
    private readonly FirefoxBidiEndpointClient _firefoxEndpointClient = new();
    private readonly SelectorInspectionService _inspectionService = new();
    private readonly FirefoxBidiInspectionService _firefoxInspectionService = new();
    private readonly JsonSettingsStore _settingsStore = new(GetSettingsPath());
    private readonly List<string> _history = new();
    private readonly List<CaptureFileRecord> _captureFiles = new();
    private readonly List<CaptureFileRecord> _filteredCaptureFiles = new();
    private readonly DispatcherTimer _autoLensTimer;
    private SelectorInspectionResult? _currentInspection;
    private LensWindow? _lensWindow;
    private LensFrameState? _lastLensState;
    private Window? _activeResultWindow;
    private ImageResourcesWindow? _imageResourcesWindow;
    private string? _activeResultKind;
    private BrowserKind? _activeBrowser;
    private WebImageResource? _lastLensPreviewImage;
    private bool _isInspectingLens;
    private bool _isCompactMode;
    private bool _isApplyingSettings;
    private string? _lastLiveLensHistoryKey;
    private AppSettings _settings = AppSettings.Default;
    private IReadOnlyList<UsageProfileDefinition> _usageProfiles = Array.Empty<UsageProfileDefinition>();

    public MainWindow()
    {
        InitializeComponent();
        InitializeCaptureFilters();
        InitializeUsageProfiles();
        _autoLensTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(550)
        };
        _autoLensTimer.Tick += AutoLensTimer_Tick;
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
    }

    private async void LaunchChromeButton_Click(object sender, RoutedEventArgs e) => await LaunchBrowserAsync(BrowserKind.Chrome);

    private void HeaderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "▢";
    }

    private async void LaunchEdgeButton_Click(object sender, RoutedEventArgs e) => await LaunchBrowserAsync(BrowserKind.Edge);

    private async void LaunchOperaButton_Click(object sender, RoutedEventArgs e) => await LaunchBrowserAsync(BrowserKind.Opera);

    private async void LaunchFirefoxButton_Click(object sender, RoutedEventArgs e) => await LaunchBrowserAsync(BrowserKind.Firefox);

    private async void RefreshTargetsButton_Click(object sender, RoutedEventArgs e) => await RefreshTargetsAsync();

    private async void InspectButton_Click(object sender, RoutedEventArgs e) => await InspectSelectedTargetAsync();

    private void LensButton_Click(object sender, RoutedEventArgs e) => ToggleLens();

    private void HelpButton_Click(object sender, RoutedEventArgs e) => OpenHelp();

    private async void SettingsButton_Click(object sender, RoutedEventArgs e) => await OpenSettingsAsync();

    private void ShowDomButton_Click(object sender, RoutedEventArgs e) => ShowDomWindow();

    private void ShowCssButton_Click(object sender, RoutedEventArgs e) => ShowCssWindow();

    private void ShowConsoleButton_Click(object sender, RoutedEventArgs e) => ShowResultWindow("Console", ConsoleTextBox.Text);

    private void ShowAttributesButton_Click(object sender, RoutedEventArgs e) => ShowResultWindow("Attributes", AttributesTextBox.Text);

    private async void ShowImagesButton_Click(object sender, RoutedEventArgs e) => await ShowImagesWindowAsync();

    private void ShowIssuesButton_Click(object sender, RoutedEventArgs e) => ShowIssuesWindow();

    private async void CaptureLensButton_Click(object sender, RoutedEventArgs e) => await CaptureLensAsync();

    private void OpenCaptureButton_Click(object sender, RoutedEventArgs e) => OpenSelectedCapture();

    private void RenameCaptureButton_Click(object sender, RoutedEventArgs e) => RenameSelectedCapture();

    private void DeleteCaptureButton_Click(object sender, RoutedEventArgs e) => DeleteSelectedCapture();

    private void RefreshCapturesButton_Click(object sender, RoutedEventArgs e) => LoadCaptures();

    private void EditEvidenceNotesButton_Click(object sender, RoutedEventArgs e) => EditSelectedEvidenceNotes();

    private void CompareCapturesButton_Click(object sender, RoutedEventArgs e) => CompareCaptures();

    private void ExportReportButton_Click(object sender, RoutedEventArgs e) => ExportSelectedCaptureReport();

    private async void UsageProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        if (UsageProfileComboBox.SelectedItem is not UsageProfileDefinition profile)
        {
            return;
        }

        _settings = _settings with
        {
            Ui = _settings.Ui with
            {
                Profile = profile.Profile
            }
        };
        ApplyUsageProfile(profile, selectPriorityTab: true);
        await SaveSettingsAsync();
        SetStatus($"Profile active: {profile.DisplayName}.");
    }

    private void CaptureFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCaptureNotesPreview();

    private void CaptureSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyCaptureFilters();

    private void CaptureFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyCaptureFilters();

    private void ClearCaptureFiltersButton_Click(object sender, RoutedEventArgs e) => ClearCaptureFilters();

    private void CopyHtmlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInspection is null)
        {
            SetStatus("No active inspection.");
            return;
        }

        System.Windows.Clipboard.SetText(_currentInspection.OuterHtml);
        SetStatus("HTML copied.");
    }

    private void CopyCssButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInspection is null)
        {
            SetStatus("No active inspection.");
            return;
        }

        System.Windows.Clipboard.SetText(BuildComputedCss(_currentInspection));
        SetStatus("Computed CSS copied.");
    }

    private void ExportJsonButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInspection is null)
        {
            SetStatus("No active inspection.");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export inspection",
            Filter = "JSON (*.json)|*.json",
            FileName = $"julco-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var json = JsonSerializer.Serialize(
            _currentInspection,
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(dialog.FileName, json);
        SetStatus($"Exported: {dialog.FileName}");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsAsync();
        LoadCaptures();
        var screens = Forms.Screen.AllScreens;
        if (screens.Length <= 1)
        {
            EnableCompactMode();
            return;
        }

        EnableMultiMonitorMode(screens);
    }

    private async Task LaunchBrowserAsync(BrowserKind browserKind)
    {
        try
        {
            SetBusy(true, $"Opening {browserKind} with remote inspection...");
            var port = GetPort();
            var process = browserKind switch
            {
                BrowserKind.Chrome => _browserLauncher.LaunchChrome(port, "https://example.com"),
                BrowserKind.Edge => _browserLauncher.LaunchEdge(port, "https://example.com"),
                BrowserKind.Opera => _browserLauncher.LaunchOpera(port, "https://example.com"),
                BrowserKind.Firefox => _browserLauncher.LaunchFirefox(port, "https://example.com"),
                _ => null
            };

            if (process is null)
            {
                SetStatus($"{browserKind} was not found.");
                return;
            }

            await Task.Delay(1200);
            _activeBrowser = browserKind;
            PortLabelTextBlock.Text = browserKind == BrowserKind.Firefox ? "BiDi" : "CDP";
            await RefreshTargetsAsync();
            SetStatus($"{browserKind} opened on port {port}.");
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshTargetsAsync()
    {
        try
        {
            var isFirefox = _activeBrowser == BrowserKind.Firefox;
            SetBusy(true, isFirefox ? "Reading Firefox BiDi tabs..." : "Reading CDP tabs...");
            var targets = isFirefox
                ? await _firefoxEndpointClient.GetPageTargetsAsync(GetPort(), CancellationToken.None)
                : await _endpointClient.GetPageTargetsAsync(GetPort(), CancellationToken.None);
            TargetsComboBox.ItemsSource = targets;

            if (targets.Count > 0)
            {
                TargetsComboBox.SelectedIndex = 0;
                SetStatus($"{targets.Count} tab(s) detected.");
            }
            else
            {
                SetStatus("No inspectable web tabs found. Open an http/https page in the browser started by Julco.");
            }
        }
        catch (Exception exception)
        {
            SetStatus($"Could not read browser tabs: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task InspectSelectedTargetAsync()
    {
        if (TargetsComboBox.SelectedItem is not CdpTarget target)
        {
            SetStatus("Select a tab first.");
            return;
        }

        var selector = string.IsNullOrWhiteSpace(SelectorTextBox.Text)
            ? "body"
            : SelectorTextBox.Text.Trim();

        try
        {
            SetBusy(true, $"Inspecting {selector}...");
            var result = await InspectSelectorAsync(target, selector, CancellationToken.None);
            ShowInspection(target, result);
            AddHistory($"{DateTime.Now:HH:mm:ss}  {result.TagName}  {selector}");
            SetStatus("Inspection completed.");
        }
        catch (Exception exception)
        {
            SetStatus($"Inspection failed: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ToggleLens()
    {
        if (_lensWindow is not null)
        {
            _lensWindow.Close();
            return;
        }

        _lensWindow = new LensWindow
        {
            Owner = this
        };
        _lensWindow.LensChanged += LensWindow_LensChanged;
        _lensWindow.InspectCenterRequested += LensWindow_InspectCenterRequested;
        _lensWindow.CaptureRequested += LensWindow_CaptureRequested;
        _lensWindow.Closed += LensWindow_Closed;
        _lensWindow.Show();
        PlaceLensNearMainWindow(_lensWindow);
        LensButtonTextBlock.Text = "Close";
        SetStatus("Lens active. Move or resize it; Julco will inspect the center automatically. Right-click the lens to close it.");
        ScheduleAutoLensInspection();
    }

    private void LensWindow_LensChanged(object? sender, LensFrameChangedEventArgs e)
    {
        _lastLensState = e.State;
        _lastLensPreviewImage = null;
        LensStateTextBlock.Text =
            $"Center {e.State.CenterPoint.X:0},{e.State.CenterPoint.Y:0} | Frame {e.State.Bounds.Width:0}x{e.State.Bounds.Height:0}";
        ScheduleAutoLensInspection();
    }

    private async void LensWindow_InspectCenterRequested(object? sender, LensFrameState state) => await InspectLensCenterAsync(state);

    private async void LensWindow_CaptureRequested(object? sender, LensFrameState state)
    {
        _lastLensState = state;
        await CaptureLensAsync();
    }

    private void LensWindow_Closed(object? sender, EventArgs e)
    {
        if (_lensWindow is not null)
        {
            _lensWindow.LensChanged -= LensWindow_LensChanged;
            _lensWindow.InspectCenterRequested -= LensWindow_InspectCenterRequested;
            _lensWindow.CaptureRequested -= LensWindow_CaptureRequested;
            _lensWindow.Closed -= LensWindow_Closed;
        }

        _lensWindow = null;
        _lastLiveLensHistoryKey = null;
        LensButtonTextBlock.Text = "Lens";
        LensStateTextBlock.Text = "Inactive";
        _autoLensTimer.Stop();
        SetStatus("Lens closed.");
    }

    private async Task InspectLensCenterAsync(LensFrameState state)
    {
        if (_isInspectingLens)
        {
            return;
        }

        if (TargetsComboBox.SelectedItem is not CdpTarget target)
        {
            SetStatus("Select a browser tab before inspecting with the lens.");
            return;
        }

        try
        {
            _isInspectingLens = true;
            SetStatus($"Inspecting center {state.CenterPoint.X:0},{state.CenterPoint.Y:0}...");
            var result = await InspectScreenPointAsync(
                target,
                state,
                CancellationToken.None);

            ShowInspection(target, result);
            var historyKey = $"{result.TagName}|{result.Selector}";
            if (!string.Equals(_lastLiveLensHistoryKey, historyKey, StringComparison.Ordinal))
            {
                _lastLiveLensHistoryKey = historyKey;
                AddHistory($"{DateTime.Now:HH:mm:ss}  {result.TagName}  live lens");
            }

            SetStatus("Lens inspection completed.");
        }
        catch (Exception exception)
        {
            SetStatus($"Lens: {exception.Message}");
        }
        finally
        {
            _isInspectingLens = false;
        }
    }

    private void ShowInspection(CdpTarget target, SelectorInspectionResult result)
    {
        _currentInspection = result;
        ElementTextBlock.Text = $"{result.TagName}  |  {result.Selector}";
        SelectorTextBox.Text = result.Selector;
        UrlTextBlock.Text = target.Url;
        var domSummary = DomSummaryBuilder.Build(
            result.TagName,
            result.Selector,
            result.OuterHtml,
            result.Attributes);
        DomSummaryTextBox.Text = DomSummaryBuilder.ToDisplayText(domSummary);
        DomImportantAttributesGrid.ItemsSource = domSummary.ImportantAttributes;
        DomTextBox.Text = DomFormatter.PrettyPrint(result.OuterHtml);
        CssExplanationGrid.ItemsSource = CssExplanationBuilder.Build(result.ComputedStyle);
        ComputedTextBox.Text = BuildComputedCss(result);
        var commonIssues = CommonIssueDetector.Detect(result);
        IssuesGrid.ItemsSource = commonIssues;
        IssuesTextBox.Text = CommonIssueDetector.BuildReport(commonIssues);
        IssuesSummaryTextBlock.Text = commonIssues.Count == 0
            ? "No common issues detected."
            : $"{commonIssues.Count} common issue(s) detected.";
        RulesTextBox.Text = string.Join(Environment.NewLine, result.MatchedCssRules);
        ConsoleTextBox.Text = result.ConsoleMessages.Count == 0
            ? "No messages captured during the connection."
            : string.Join(Environment.NewLine, result.ConsoleMessages);
        AttributesTextBox.Text = string.Join(
            Environment.NewLine,
            result.Attributes.Select(item => $"{item.Key}=\"{item.Value}\""));
        _imageResourcesWindow?.SetImages(BuildImagesWithLensPreview(result.Images));
    }

    private Task<SelectorInspectionResult> InspectSelectorAsync(
        CdpTarget target,
        string selector,
        CancellationToken cancellationToken)
    {
        return IsFirefoxTarget(target)
            ? _firefoxInspectionService.InspectAsync(target, selector, cancellationToken)
            : _inspectionService.InspectAsync(target, selector, cancellationToken);
    }

    private Task<SelectorInspectionResult> InspectScreenPointAsync(
        CdpTarget target,
        LensFrameState state,
        CancellationToken cancellationToken)
    {
        return IsFirefoxTarget(target)
            ? _firefoxInspectionService.InspectScreenPointAsync(
                target,
                state.CenterPoint.X,
                state.CenterPoint.Y,
                state.Bounds.X,
                state.Bounds.Y,
                state.Bounds.Width,
                state.Bounds.Height,
                cancellationToken)
            : _inspectionService.InspectScreenPointAsync(
                target,
                state.CenterPoint.X,
                state.CenterPoint.Y,
                state.Bounds.X,
                state.Bounds.Y,
                state.Bounds.Width,
                state.Bounds.Height,
                cancellationToken);
    }

    private static bool IsFirefoxTarget(CdpTarget target)
    {
        return target.Type.Equals("firefox-page", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CaptureLensAsync()
    {
        if (_lensWindow is null || _lastLensState is null)
        {
            SetStatus("Open the lens before creating a capture.");
            return;
        }

        if (TargetsComboBox.SelectedItem is not CdpTarget target)
        {
            SetStatus("Select a browser tab before creating a capture.");
            return;
        }

        var notes = PromptForEvidenceNotes();

        try
        {
            SetBusy(true, "Creating evidence package...");
            var state = _lastLensState;
            var inspection = await InspectScreenPointAsync(
                target,
                state,
                CancellationToken.None);

            ShowInspection(target, inspection);

            var captureRoot = GetCaptureRootDirectory();
            Directory.CreateDirectory(captureRoot);

            var folderName = BuildCaptureFolderName(target, inspection);
            var captureDirectory = UniqueDirectory(Path.Combine(captureRoot, folderName));
            Directory.CreateDirectory(captureDirectory);

            var screenshotPath = Path.Combine(captureDirectory, "screenshot.png");
            var screenshotBytes = await CaptureRegionAsync(state, screenshotPath);
            _lastLensPreviewImage = CreateLensPreviewImage(state, screenshotBytes);
            var evidenceImages = BuildImagesWithLensPreview(inspection.Images);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(
                Path.Combine(captureDirectory, "inspection.json"),
                JsonSerializer.Serialize(inspection, jsonOptions));
            File.WriteAllText(Path.Combine(captureDirectory, "dom.html"), inspection.OuterHtml);
            File.WriteAllText(Path.Combine(captureDirectory, "computed.css"), BuildComputedCss(inspection));
            File.WriteAllText(Path.Combine(captureDirectory, "console.txt"), string.Join(Environment.NewLine, inspection.ConsoleMessages));
            File.WriteAllText(
                Path.Combine(captureDirectory, "attributes.txt"),
                string.Join(Environment.NewLine, inspection.Attributes.Select(item => $"{item.Key}=\"{item.Value}\"")));
            File.WriteAllText(
                Path.Combine(captureDirectory, "image-resources.json"),
                JsonSerializer.Serialize(evidenceImages, jsonOptions));
            var commonIssues = CommonIssueDetector.Detect(inspection);
            File.WriteAllText(
                Path.Combine(captureDirectory, "common-issues.json"),
                JsonSerializer.Serialize(commonIssues, jsonOptions));
            File.WriteAllText(
                Path.Combine(captureDirectory, "common-issues.md"),
                CommonIssueDetector.BuildReport(commonIssues));

            var evidence = BuildEvidencePackage(
                target,
                inspection,
                state,
                notes,
                "screenshot.png",
                "inspection.json",
                "dom.html",
                "computed.css",
                "console.txt",
                "attributes.txt",
                "image-resources.json");
            SaveCaptureNotes(captureDirectory, notes);
            File.WriteAllText(
                Path.Combine(captureDirectory, "evidence.json"),
                JsonSerializer.Serialize(evidence, jsonOptions));
            File.WriteAllText(
                Path.Combine(captureDirectory, "evidence-summary.md"),
                BuildEvidenceMarkdown(evidence));

            var manifest = new CaptureManifest(
                DateTimeOffset.Now,
                target.Title,
                target.Url,
                inspection.TagName,
                inspection.Selector,
                state.Bounds.X,
                state.Bounds.Y,
                state.Bounds.Width,
                state.Bounds.Height,
                "screenshot.png",
                "inspection.json");

            File.WriteAllText(
                Path.Combine(captureDirectory, "manifest.json"),
                JsonSerializer.Serialize(manifest, jsonOptions));

            LoadCaptures();
            SelectCapture(captureDirectory);
            SetStatus($"Evidence package saved: {captureDirectory}");
        }
        catch (Exception exception)
        {
            SetStatus($"Evidence capture failed: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private CaptureNotes PromptForEvidenceNotes(CaptureNotes? existingNotes = null)
    {
        var window = new EvidenceNotesWindow(existingNotes ?? CaptureNotes.Empty)
        {
            Owner = this,
            Topmost = _settings.Ui.KeepResultWindowsTopmost
        };

        return window.ShowDialog() == true
            ? window.Notes
            : existingNotes ?? CaptureNotes.Empty;
    }

    private EvidencePackage BuildEvidencePackage(
        CdpTarget target,
        SelectorInspectionResult inspection,
        LensFrameState state,
        CaptureNotes notes,
        string screenshotFile,
        string inspectionFile,
        string domFile,
        string computedCssFile,
        string consoleFile,
        string attributesFile,
        string imagesFile)
    {
        var now = DateTimeOffset.Now;
        var browser = _activeBrowser?.ToString() ?? InferBrowserName(target);
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(
            (int)Math.Round(state.CenterPoint.X),
            (int)Math.Round(state.CenterPoint.Y)));

        return new EvidencePackage(
            "1.0",
            now,
            new EvidenceBrowserContext(
                browser,
                target.Type,
                PortTextBox.Text.Trim(),
                target.Id),
            new EvidencePageContext(
                target.Title,
                target.Url),
            new EvidenceElementContext(
                inspection.TagName,
                inspection.Selector,
                inspection.Attributes,
                inspection.Images.Count,
                inspection.ConsoleMessages.Count),
            new EvidenceFrameContext(
                state.Bounds.X,
                state.Bounds.Y,
                state.Bounds.Width,
                state.Bounds.Height,
                state.CenterPoint.X,
                state.CenterPoint.Y,
                screen.DeviceName,
                screen.Bounds.Width,
                screen.Bounds.Height),
            new EvidenceFiles(
                screenshotFile,
                inspectionFile,
                domFile,
                computedCssFile,
                consoleFile,
                attributesFile,
                imagesFile,
                "capture-notes.json",
                "notes.md",
                "evidence-summary.md"),
            notes.ToEvidenceText(),
            notes);
    }

    private static string InferBrowserName(CdpTarget target)
    {
        if (target.Type.Equals("firefox-page", StringComparison.OrdinalIgnoreCase))
        {
            return "Firefox";
        }

        return "Chromium-compatible";
    }

    private static string BuildEvidenceMarkdown(EvidencePackage evidence)
    {
        var lines = new List<string>
        {
            "# Julco Evidence Package",
            string.Empty,
            "## Summary",
            $"- Created: {evidence.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"- Browser: {evidence.Browser.Name}",
            $"- Remote port: {evidence.Browser.RemotePort}",
            $"- Page title: {NormalizeMarkdownLine(evidence.Page.Title)}",
            $"- URL: {NormalizeMarkdownLine(evidence.Page.Url)}",
            $"- Element: {evidence.Element.TagName}  {NormalizeMarkdownLine(evidence.Element.Selector)}",
            $"- Lens frame: {evidence.Frame.Width:0}x{evidence.Frame.Height:0} at {evidence.Frame.X:0},{evidence.Frame.Y:0}",
            $"- Center: {evidence.Frame.CenterX:0},{evidence.Frame.CenterY:0}",
            $"- Screen: {NormalizeMarkdownLine(evidence.Frame.ScreenName)} ({evidence.Frame.ScreenWidth}x{evidence.Frame.ScreenHeight})",
            string.Empty,
            "## Notes",
            BuildNotesMarkdown(evidence),
            string.Empty,
            "## Files",
            $"- Screenshot: `{evidence.Files.Screenshot}`",
            $"- Full inspection JSON: `{evidence.Files.Inspection}`",
            $"- DOM: `{evidence.Files.Dom}`",
            $"- Computed CSS: `{evidence.Files.ComputedCss}`",
            $"- Console: `{evidence.Files.Console}`",
            $"- Attributes: `{evidence.Files.Attributes}`",
            $"- Image resources: `{evidence.Files.Images}`",
            $"- Structured notes: `{evidence.Files.StructuredNotes}`",
            $"- Notes: `{evidence.Files.Notes}`",
            string.Empty,
            "## Element Attributes"
        };

        if (evidence.Element.Attributes.Count == 0)
        {
            lines.Add("_No attributes captured._");
        }
        else
        {
            foreach (var attribute in evidence.Element.Attributes.OrderBy(item => item.Key))
            {
                lines.Add($"- `{attribute.Key}`: {NormalizeMarkdownLine(attribute.Value)}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Counts");
        lines.Add($"- Console messages: {evidence.Element.ConsoleMessageCount}");
        lines.Add($"- Image resources: {evidence.Element.ImageResourceCount}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildNotesMarkdown(EvidencePackage evidence)
    {
        if (evidence.StructuredNotes is not null && evidence.StructuredNotes.HasContent)
        {
            return string.Join(
                Environment.NewLine,
                $"- Category: {evidence.StructuredNotes.Category}",
                $"- Severity: {evidence.StructuredNotes.Severity}",
                $"- Status: {evidence.StructuredNotes.Status}",
                $"- Tags: {NormalizeMarkdownLine(evidence.StructuredNotes.Tags)}",
                string.Empty,
                evidence.StructuredNotes.Observation.Trim());
        }

        return string.IsNullOrWhiteSpace(evidence.Notes) ? "_No notes added._" : evidence.Notes;
    }

    private static string NormalizeMarkdownLine(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.ReplaceLineEndings(" ").Trim();
    }

    private static CaptureNotes LoadCaptureNotes(string captureDirectory)
    {
        var structuredNotesPath = Path.Combine(captureDirectory, "capture-notes.json");
        if (File.Exists(structuredNotesPath))
        {
            try
            {
                var notes = JsonSerializer.Deserialize<CaptureNotes>(File.ReadAllText(structuredNotesPath));
                if (notes is not null)
                {
                    return notes;
                }
            }
            catch (JsonException)
            {
            }
        }

        var notesPath = Path.Combine(captureDirectory, "notes.md");
        if (File.Exists(notesPath))
        {
            var legacyNotes = ExtractObservationFromNotesMarkdown(File.ReadAllText(notesPath));
            if (!string.IsNullOrWhiteSpace(legacyNotes))
            {
                return CaptureNotes.Empty with
                {
                    Observation = legacyNotes,
                    UpdatedAt = new DateTimeOffset(File.GetLastWriteTime(notesPath))
                };
            }
        }

        return CaptureNotes.Empty;
    }

    private static string ExtractObservationFromNotesMarkdown(string content)
    {
        var text = content.Trim();
        const string observationHeader = "## Observation";
        var observationIndex = text.IndexOf(observationHeader, StringComparison.OrdinalIgnoreCase);
        if (observationIndex < 0)
        {
            return text;
        }

        return text[(observationIndex + observationHeader.Length)..]
            .Replace("_No observation added._", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static void SaveCaptureNotes(string captureDirectory, CaptureNotes notes)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(captureDirectory, "capture-notes.json"),
            JsonSerializer.Serialize(notes, jsonOptions));
        File.WriteAllText(
            Path.Combine(captureDirectory, "notes.md"),
            notes.ToMarkdown());
    }

    private async Task<byte[]> CaptureRegionAsync(LensFrameState state, string screenshotPath)
    {
        var bytes = await CaptureRegionBytesAsync(state, hideLens: true);
        await File.WriteAllBytesAsync(screenshotPath, bytes);
        return bytes;
    }

    private async Task<byte[]> CaptureRegionBytesAsync(LensFrameState state, bool hideLens)
    {
        var wasVisible = _lensWindow?.IsVisible == true;
        if (hideLens && wasVisible)
        {
            _lensWindow!.Hide();
            await Task.Delay(120);
        }

        try
        {
            var x = (int)Math.Round(state.Bounds.X);
            var y = (int)Math.Round(state.Bounds.Y);
            var width = Math.Max(1, (int)Math.Round(state.Bounds.Width));
            var height = Math.Max(1, (int)Math.Round(state.Bounds.Height));

            using var bitmap = new System.Drawing.Bitmap(width, height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
        finally
        {
            if (hideLens && wasVisible)
            {
                _lensWindow!.Show();
            }
        }
    }

    private void ScheduleAutoLensInspection()
    {
        if (_lensWindow is null || _lastLensState is null)
        {
            return;
        }

        _autoLensTimer.Stop();
        _autoLensTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.Ui.LensInspectionDelayMs, 150, 260));
        _autoLensTimer.Start();
    }

    private async void AutoLensTimer_Tick(object? sender, EventArgs e)
    {
        _autoLensTimer.Stop();
        if (_lastLensState is not null)
        {
            await InspectLensCenterAsync(_lastLensState);
        }
    }

    private void ShowDomWindow()
    {
        if (_currentInspection is null || string.IsNullOrWhiteSpace(_currentInspection.OuterHtml))
        {
            SetStatus("No DOM to show.");
            return;
        }

        ToggleResultWindow(
            "DOM",
            () => new DomResultWindow(
                _currentInspection.OuterHtml,
                _currentInspection.TagName,
                _currentInspection.Selector));
    }

    private void ShowCssWindow()
    {
        if (_currentInspection is null)
        {
            SetStatus("No CSS to show.");
            return;
        }

        ToggleResultWindow(
            "CSS",
            () => new CssResultWindow(new SelectorInspectionResultView(
                CssExplanationBuilder.Build(_currentInspection.ComputedStyle),
                BuildComputedCss(_currentInspection))));
    }

    private void ShowIssuesWindow()
    {
        if (_currentInspection is null)
        {
            SetStatus("No active inspection.");
            return;
        }

        ToggleResultWindow(
            "Issues",
            () => new CommonIssuesWindow(CommonIssueDetector.Detect(_currentInspection)));
    }

    private void ShowResultWindow(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            SetStatus($"No content for {title}.");
            return;
        }

        ToggleResultWindow(title, () => new ResultWindow(title, content));
    }

    private async Task ShowImagesWindowAsync()
    {
        if (_lastLensState is not null)
        {
            _lastLensPreviewImage = await CreateLensPreviewImageAsync(_lastLensState);
        }

        var images = BuildImagesWithLensPreview(_currentInspection?.Images ?? Array.Empty<WebImageResource>());
        if (_imageResourcesWindow is not null)
        {
            _imageResourcesWindow.Activate();
            _imageResourcesWindow.SetImages(images);
            return;
        }

        var window = new ImageResourcesWindow(images)
        {
            Owner = this,
            Topmost = _settings.Ui.KeepResultWindowsTopmost
        };

        _imageResourcesWindow = window;
        window.Closed += (_, _) => _imageResourcesWindow = null;
        PlaceResultWindow(window);
        window.Show();
    }

    private IReadOnlyList<WebImageResource> BuildImagesWithLensPreview(IReadOnlyList<WebImageResource> images)
    {
        if (_lastLensPreviewImage is null)
        {
            return images;
        }

        return new[] { _lastLensPreviewImage }
            .Concat(images.Where(image => !string.Equals(image.Url, _lastLensPreviewImage.Url, StringComparison.Ordinal)))
            .ToArray();
    }

    private async Task<WebImageResource?> CreateLensPreviewImageAsync(LensFrameState state)
    {
        try
        {
            var bytes = await CaptureRegionBytesAsync(state, hideLens: true);
            return CreateLensPreviewImage(state, bytes);
        }
        catch (Exception exception)
        {
            SetStatus($"Lens preview unavailable: {exception.Message}");
            return null;
        }
    }

    private static WebImageResource CreateLensPreviewImage(LensFrameState state, byte[] bytes)
    {
        var width = Math.Max(1, (int)Math.Round(state.Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(state.Bounds.Height));
        return new WebImageResource(
            $"data:image/png;base64,{Convert.ToBase64String(bytes)}",
            "lens-frame",
            "png",
            "Lens frame",
            width,
            height,
            false,
            width,
            height,
            width,
            height,
            bytes.Length,
            true);
    }

    private void ToggleResultWindow(string kind, Func<Window> createWindow)
    {
        if (_activeResultWindow is not null)
        {
            if (_activeResultKind == kind)
            {
                _activeResultWindow.Close();
                ClearActiveResultWindow();
                return;
            }

            _activeResultWindow.Close();
            ClearActiveResultWindow();
        }

        var window = createWindow();
        _activeResultWindow = window;
        _activeResultKind = kind;
        window.Owner = this;
        window.Topmost = _settings.Ui.KeepResultWindowsTopmost;
        window.Closed += ActiveResultWindow_Closed;
        PlaceResultWindow(window);
        window.Show();
    }

    private void ActiveResultWindow_Closed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _activeResultWindow))
        {
            ClearActiveResultWindow();
        }
    }

    private void ClearActiveResultWindow()
    {
        if (_activeResultWindow is not null)
        {
            _activeResultWindow.Closed -= ActiveResultWindow_Closed;
        }

        _activeResultWindow = null;
        _activeResultKind = null;
    }

    private void PlaceResultWindow(Window window)
    {
        if (_isCompactMode)
        {
            PlaceResultWindowForCompactMode(window);
        }
        else
        {
            PlaceResultWindowForMultiMonitor(window);
        }
    }

    private void EnableCompactMode()
    {
        var area = SystemParameters.WorkArea;
        _isCompactMode = true;
        ResultsTabControl.Visibility = Visibility.Collapsed;
        GapColumn.Width = new GridLength(0);
        ResultsColumn.Width = new GridLength(0);
        SideColumn.Width = new GridLength(1, GridUnitType.Star);
        Width = 286;
        MinWidth = 286;
        Height = area.Height;
        Top = area.Top;
        Left = Math.Max(area.Left, area.Right - Width);
        HeaderPanel.Margin = new Thickness(12, 8, 10, 8);
        HeaderActionsPanel.Margin = new Thickness(0, 8, 0, 0);
        HeaderActionsPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        Grid.SetRow(HeaderActionsPanel, 1);
        Grid.SetColumn(HeaderActionsPanel, 0);
        Grid.SetColumnSpan(HeaderActionsPanel, 3);
        Grid.SetRow(TitleBarControlsPanel, 0);
        Grid.SetColumn(TitleBarControlsPanel, 2);
        LogoImage.Width = 48;
        LogoImage.Height = 48;
        WorkspaceGrid.Margin = new Thickness(8);
        InspectorTitleTextBlock.Visibility = Visibility.Collapsed;
        InspectorHelpTextBlock.Visibility = Visibility.Collapsed;
        HistoryListBox.MaxHeight = 110;
        CaptureFilesListBox.MaxHeight = 130;
        SetCompactButtonMetrics(true);
        SetStatus("Compact mode: use the vertical controls and result buttons.");
    }

    private void EnableMultiMonitorMode(IReadOnlyList<Forms.Screen> screens)
    {
        _isCompactMode = false;
        ResultsTabControl.Visibility = Visibility.Visible;
        GapColumn.Width = new GridLength(16);
        ResultsColumn.Width = new GridLength(1, GridUnitType.Star);
        SideColumn.Width = new GridLength(330);
        InspectorTitleTextBlock.Visibility = Visibility.Visible;
        InspectorHelpTextBlock.Visibility = Visibility.Visible;
        HistoryListBox.MaxHeight = double.PositiveInfinity;
        WorkspaceGrid.Margin = new Thickness(16);
        HeaderPanel.Margin = new Thickness(14, 8, 14, 8);
        HeaderActionsPanel.Margin = new Thickness(10, 0, 0, 0);
        HeaderActionsPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        Grid.SetRow(HeaderActionsPanel, 0);
        Grid.SetColumn(HeaderActionsPanel, 1);
        Grid.SetColumnSpan(HeaderActionsPanel, 1);
        Grid.SetRow(TitleBarControlsPanel, 0);
        Grid.SetColumn(TitleBarControlsPanel, 2);
        LogoImage.Width = 62;
        LogoImage.Height = 62;
        CaptureFilesListBox.MaxHeight = double.PositiveInfinity;
        SetCompactButtonMetrics(false);

        var targetScreen = screens.FirstOrDefault(screen => !screen.Primary) ?? screens[0];
        var area = targetScreen.WorkingArea;
        Width = Math.Min(1180, Math.Max(980, area.Width - 80));
        Height = Math.Min(760, Math.Max(620, area.Height - 80));
        Left = area.Left + Math.Max(20, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(20, (area.Height - Height) / 2);
        SetStatus($"{screens.Count} monitors detected: wide mode enabled.");
    }

    private void SetCompactButtonMetrics(bool compact)
    {
        var minHeight = compact ? 26.0 : 32.0;
        var browserWidth = compact ? 32.0 : 38.0;
        var margin = compact ? new Thickness(0, 0, 5, 5) : new Thickness(0, 0, 8, 0);
        var actionMargin = compact ? new Thickness(0, 0, 3, 3) : new Thickness(0, 0, 4, 4);

        foreach (var button in new[]
        {
            LaunchChromeButton,
            LaunchEdgeButton,
            LaunchOperaButton,
            LaunchFirefoxButton,
            SettingsButton
        })
        {
            button.Width = browserWidth;
            button.MinHeight = minHeight;
            button.Margin = margin;
        }

        PortTextBox.Width = compact ? 64 : 72;
        PortTextBox.MinHeight = compact ? 26 : 30;
        PortTextBox.Margin = compact ? new Thickness(0, 0, 5, 5) : new Thickness(0, 0, 8, 0);
        PortLabelTextBlock.Margin = compact ? new Thickness(0, 0, 5, 5) : new Thickness(0, 0, 8, 0);
        RefreshTargetsButton.MinHeight = minHeight;
        RefreshTargetsButton.Padding = compact ? new Thickness(9, 3, 9, 3) : new Thickness(12, 6, 12, 6);
        RefreshTargetsButton.Margin = margin;
        LensButton.MinHeight = minHeight;
        LensButton.Padding = compact ? new Thickness(8, 3, 8, 3) : new Thickness(12, 6, 12, 6);
        LensButton.Margin = margin;
        CaptureActionsGrid.Columns = compact ? 3 : 2;
        ResultActionsGrid.Columns = compact ? 3 : 2;
        RenameCaptureButton.Content = compact ? "Name" : "Rename";
        DeleteCaptureButton.Content = compact ? "Del" : "Delete";
        RefreshCapturesButton.Content = compact ? "Load" : "Reload";
        EditEvidenceNotesButton.Content = compact ? "Note" : "Notes";
        CompareCapturesButton.Content = compact ? "Diff" : "Compare";
        ExportReportButton.Content = compact ? "Rpt" : "Report";
        ShowIssuesButton.Content = compact ? "Audit" : "Issues";

        foreach (var button in GetButtons(CaptureActionsGrid).Concat(GetButtons(ResultActionsGrid)))
        {
            button.MinHeight = compact ? 26 : 32;
            button.Padding = compact ? new Thickness(8, 3, 8, 3) : new Thickness(12, 6, 12, 6);
            button.Margin = actionMargin;
            button.FontSize = compact ? 11 : 12;
        }

        foreach (var button in GetButtons(ActionButtonsPanel))
        {
            button.MinHeight = compact ? 28 : 32;
            button.FontSize = compact ? 11 : 12;
        }
    }

    private static IEnumerable<System.Windows.Controls.Button> GetButtons(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is System.Windows.Controls.Button button)
            {
                yield return button;
            }

            foreach (var nested in GetButtons(child))
            {
                yield return nested;
            }
        }
    }

    private Forms.Screen GetCurrentScreen()
    {
        var centerX = (int)(Left + ActualWidth / 2);
        var centerY = (int)(Top + ActualHeight / 2);
        return Forms.Screen.FromPoint(new System.Drawing.Point(centerX, centerY));
    }

    private void PlaceLensNearMainWindow(LensWindow lensWindow)
    {
        var area = GetCurrentScreen().WorkingArea;
        var gap = 16;
        var lensWidth = lensWindow.Width;
        var lensHeight = lensWindow.Height;
        var mainCenterX = Left + ActualWidth / 2;
        var screenCenterX = area.Left + area.Width / 2.0;
        var placeLeft = mainCenterX >= screenCenterX;

        var preferredLeft = placeLeft
            ? Left - lensWidth - gap
            : Left + ActualWidth + gap;

        if (preferredLeft < area.Left + 12 || preferredLeft + lensWidth > area.Right - 12)
        {
            preferredLeft = placeLeft
                ? area.Left + 20
                : area.Right - lensWidth - 20;
        }

        lensWindow.Left = Math.Clamp(preferredLeft, area.Left + 8, area.Right - lensWidth - 8);
        lensWindow.Top = Math.Clamp(Top + 20, area.Top + 8, area.Bottom - lensHeight - 8);
    }

    private void PlaceResultWindowForCompactMode(Window window)
    {
        var area = GetCurrentScreen().WorkingArea;
        window.Width = Math.Min(820, Math.Max(420, area.Width - Width - 56));
        window.Height = Math.Min(680, area.Height - 80);

        var leftSide = Left - window.Width - 12;
        var rightSide = Left + Width + 12;
        window.Left = leftSide >= area.Left
            ? leftSide
            : Math.Min(rightSide, area.Right - window.Width - 12);
        window.Top = Math.Clamp(Top, area.Top + 12, area.Bottom - window.Height - 12);
    }

    private void PlaceResultWindowForMultiMonitor(Window window)
    {
        var screens = Forms.Screen.AllScreens;
        var mainScreen = GetCurrentScreen();
        var targetScreen = screens.FirstOrDefault(screen => screen.DeviceName != mainScreen.DeviceName)
            ?? mainScreen;
        var area = targetScreen.WorkingArea;
        window.Width = Math.Min(900, area.Width - 80);
        window.Height = Math.Min(680, area.Height - 80);
        window.Left = area.Left + Math.Max(20, (area.Width - window.Width) / 2);
        window.Top = area.Top + Math.Max(20, (area.Height - window.Height) / 2);
    }

    private int GetPort()
    {
        if (int.TryParse(PortTextBox.Text, out var port) && port > 0)
        {
            return port;
        }

        PortTextBox.Text = "9222";
        return 9222;
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            _settings = NormalizeSettings(await _settingsStore.LoadAsync(CancellationToken.None));
        }
        catch (Exception exception)
        {
            _settings = AppSettings.Default;
            SetStatus($"Settings could not be loaded: {exception.Message}");
        }

        ApplySettingsToUi();
    }

    private async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(_settings, CancellationToken.None);
    }

    private static AppSettings NormalizeSettings(AppSettings settings)
    {
        return settings with
        {
            Language = string.IsNullOrWhiteSpace(settings.Language)
                ? AppSettings.Default.Language
                : settings.Language,
            Capture = settings.Capture ?? CaptureSettings.Default,
            Export = settings.Export ?? ExportSettings.Default,
            History = settings.History ?? HistorySettings.Default,
            Ui = settings.Ui ?? UiSettings.Default
        };
    }

    private void ApplySettingsToUi()
    {
        _isApplyingSettings = true;
        try
        {
            PortTextBox.Text = _settings.Ui.CdpPort.ToString();
            _autoLensTimer.Interval = TimeSpan.FromMilliseconds(_settings.Ui.LensInspectionDelayMs);
            UsageProfileComboBox.SelectedItem = _usageProfiles.FirstOrDefault(profile => profile.Profile == _settings.Ui.Profile)
                ?? _usageProfiles.First(profile => profile.Profile == UiSettings.Default.Profile);
            ApplyTheme();
            ApplyUsageProfile(GetActiveUsageProfile(), selectPriorityTab: false);
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void InitializeUsageProfiles()
    {
        _usageProfiles = new[]
        {
            new UsageProfileDefinition(
                UsageProfile.QA,
                "QA",
                "Prioritizes reproducible issues, console signals, evidence packages, notes, and before/after comparison.",
                "Check issues first, confirm console messages, capture evidence, add notes, then compare fixes.",
                new[] { "Issues", "Console", "DOM", "Computed", "Attributes", "CSS Rules" },
                new[] { "Evidence capture", "Notes", "Compare", "Report" }),
            new UsageProfileDefinition(
                UsageProfile.Frontend,
                "Frontend",
                "Prioritizes structure, selectors, computed CSS, matched rules, and fast copy/export actions.",
                "Inspect selectors, review DOM and computed CSS, then copy exact HTML/CSS when implementing fixes.",
                new[] { "Computed", "CSS Rules", "DOM", "Attributes", "Console", "Issues" },
                new[] { "CSS", "DOM", "Copy CSS", "Copy HTML", "Report" }),
            new UsageProfileDefinition(
                UsageProfile.DesignReview,
                "Design review",
                "Prioritizes the lens frame, screenshot, image resources, sizing, visual attributes, and polished reports.",
                "Frame the visual area, check images and sizing, capture evidence, then export a shareable report.",
                new[] { "Attributes", "Computed", "Issues", "DOM", "CSS Rules", "Console" },
                new[] { "Images", "Evidence capture", "Report", "Compare" }),
            new UsageProfileDefinition(
                UsageProfile.Accessibility,
                "Accessibility",
                "Prioritizes labels, alt text, roles, keyboard/accessibility risks, visibility, and contrast issues.",
                "Start with detected issues, verify attributes and DOM semantics, then document impact in notes.",
                new[] { "Issues", "Attributes", "DOM", "Computed", "Console", "CSS Rules" },
                new[] { "Issues", "Attributes", "Notes", "Report" })
        };

        UsageProfileComboBox.ItemsSource = _usageProfiles;
    }

    private UsageProfileDefinition GetActiveUsageProfile()
    {
        return UsageProfileComboBox.SelectedItem as UsageProfileDefinition
            ?? _usageProfiles.FirstOrDefault(profile => profile.Profile == _settings.Ui.Profile)
            ?? _usageProfiles.First(profile => profile.Profile == UiSettings.Default.Profile);
    }

    private void ApplyUsageProfile(UsageProfileDefinition profile, bool selectPriorityTab)
    {
        UsageProfileHintTextBlock.Text = profile.Guidance;
        InspectorHelpTextBlock.Text = profile.InspectorHelp;
        CaptureLensButton.ToolTip = $"Evidence capture. {profile.Guidance}";
        ExportReportButton.ToolTip = $"Export a polished report prioritized for {profile.DisplayName}.";
        ReorderResultTabs(profile.TabPriority);
        ApplyButtonPriority(profile.PrimaryActions);

        if (selectPriorityTab && ResultsTabControl.Items.Count > 0)
        {
            ResultsTabControl.SelectedIndex = 0;
        }
    }

    private void ReorderResultTabs(IReadOnlyList<string> priority)
    {
        var tabs = new Dictionary<string, TabItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOM"] = DomTabItem,
            ["Computed"] = ComputedTabItem,
            ["CSS Rules"] = CssRulesTabItem,
            ["Console"] = ConsoleTabItem,
            ["Attributes"] = AttributesTabItem,
            ["Issues"] = IssuesTabItem
        };

        var selected = ResultsTabControl.SelectedItem;
        ResultsTabControl.Items.Clear();
        foreach (var tabName in priority.Where(tabs.ContainsKey))
        {
            ResultsTabControl.Items.Add(tabs[tabName]);
        }

        foreach (var tab in tabs.Values.Where(tab => !ResultsTabControl.Items.Contains(tab)))
        {
            ResultsTabControl.Items.Add(tab);
        }

        if (selected is TabItem selectedTab && ResultsTabControl.Items.Contains(selectedTab))
        {
            ResultsTabControl.SelectedItem = selectedTab;
        }
        else if (ResultsTabControl.Items.Count > 0)
        {
            ResultsTabControl.SelectedIndex = 0;
        }
    }

    private void ApplyButtonPriority(IReadOnlyList<string> primaryActions)
    {
        var labels = new Dictionary<System.Windows.Controls.Button, string>
        {
            [CaptureLensButton] = "Evidence capture",
            [EditEvidenceNotesButton] = "Notes",
            [CompareCapturesButton] = "Compare",
            [ExportReportButton] = "Report",
            [ShowImagesButton] = "Images",
            [ShowIssuesButton] = "Issues",
            [CopyHtmlButton] = "Copy HTML",
            [CopyCssButton] = "Copy CSS"
        };

        foreach (var (button, label) in labels)
        {
            var isPrimary = primaryActions.Contains(label, StringComparer.OrdinalIgnoreCase);
            button.BorderBrush = isPrimary
                ? (System.Windows.Media.Brush)Resources["Accent"]
                : (System.Windows.Media.Brush)Resources["BorderColor"];
            button.FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void ApplyTheme()
    {
        var light = ResolveThemeIsLight();
        var windowBackground = Brush(light ? "#F4F7FB" : "#101217");
        var panelBackground = Brush(light ? "#FFFFFF" : "#181B22");
        var subtlePanelBackground = Brush(light ? "#F8FAFC" : "#111318");
        var borderColor = Brush(light ? "#CFD8E3" : "#2A2F3A");
        var mutedText = Brush(light ? "#526173" : "#AAB2C0");
        var controlBackground = Brush(light ? "#EEF3F8" : "#202630");
        var controlHover = Brush(light ? "#E2EAF3" : "#2A3340");
        var controlPressed = Brush(light ? "#D4DEEA" : "#334155");
        var foreground = Brush(light ? "#121826" : "#F4F6F8");
        var inputBackground = Brush(light ? "#FFFFFF" : "#0F1218");
        var listBackground = Brush(light ? "#FFFFFF" : "#111318");
        var tabBackground = Brush(light ? "#F8FAFC" : "#101217");

        SetBrushResource("PanelBackground", panelBackground);
        SetBrushResource("BorderColor", borderColor);
        SetBrushResource("MutedText", mutedText);
        SetBrushResource("ControlBackground", controlBackground);
        SetBrushResource("ControlHover", controlHover);
        SetBrushResource("ControlPressed", controlPressed);
        SetBrushResource("InputBackground", inputBackground);
        SetBrushResource("PrimaryText", foreground);
        SetBrushResource("ListBackground", listBackground);

        Background = windowBackground;
        Foreground = foreground;
        HeaderBorder.Background = panelBackground;
        HeaderBorder.BorderBrush = borderColor;
        StatusBorder.Background = panelBackground;
        StatusBorder.BorderBrush = borderColor;
        ResultsTabControl.Background = tabBackground;
        ResultsTabControl.Foreground = foreground;
        StatusTextBlock.Foreground = mutedText;
        LogoImage.Source = Bitmap(light
            ? "/Julco.UI;component/Resources/julco-logo.png"
            : "/Julco.UI;component/Resources/julco-logo-dark.png");
        SettingsIconImage.Source = Bitmap(light
            ? "/Julco.UI;component/Resources/settings-dark.png"
            : "/Julco.UI;component/Resources/settings.png");
        LensIconImage.Source = Bitmap(light
            ? "/Julco.UI;component/Resources/Lens-dark.png"
            : "/Julco.UI;component/Resources/Lens.png");

        ApplyThemeToChildren(this, foreground, mutedText, inputBackground, listBackground, subtlePanelBackground, borderColor);
    }

    private static void ApplyThemeToChildren(
        DependencyObject parent,
        System.Windows.Media.Brush foreground,
        System.Windows.Media.Brush mutedText,
        System.Windows.Media.Brush inputBackground,
        System.Windows.Media.Brush listBackground,
        System.Windows.Media.Brush panelBackground,
        System.Windows.Media.Brush borderColor)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            switch (child)
            {
                case System.Windows.Controls.TextBox textBox:
                    textBox.Foreground = foreground;
                    textBox.CaretBrush = foreground;
                    textBox.Background = inputBackground;
                    textBox.BorderBrush = borderColor;
                    break;
                case TextBlock textBlock when textBlock.Foreground == System.Windows.SystemColors.ControlTextBrush:
                    textBlock.Foreground = foreground;
                    break;
                case System.Windows.Controls.ComboBox comboBox:
                    comboBox.Foreground = foreground;
                    comboBox.Background = inputBackground;
                    comboBox.BorderBrush = borderColor;
                    break;
                case System.Windows.Controls.ListBox listBox:
                    listBox.Foreground = foreground;
                    listBox.Background = listBackground;
                    listBox.BorderBrush = borderColor;
                    break;
                case Border border when border.BorderBrush is not null:
                    border.BorderBrush = borderColor;
                    break;
                case System.Windows.Controls.TabControl tabControl:
                    tabControl.Foreground = foreground;
                    tabControl.Background = panelBackground;
                    break;
            }

            ApplyThemeToChildren(child, foreground, mutedText, inputBackground, listBackground, panelBackground, borderColor);
        }
    }

    private bool ResolveThemeIsLight()
    {
        if (_settings.Theme == ThemeMode.Light)
        {
            return true;
        }

        if (_settings.Theme == ThemeMode.Dark)
        {
            return false;
        }

        return SystemParameters.WindowGlassColor.R
            + SystemParameters.WindowGlassColor.G
            + SystemParameters.WindowGlassColor.B > 382;
    }

    private void SetBrushResource(string key, SolidColorBrush brush)
    {
        if (Resources[key] is SolidColorBrush existing && !existing.IsFrozen)
        {
            existing.Color = brush.Color;
            return;
        }

        Resources[key] = brush;
    }

    private static SolidColorBrush Brush(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }

    private static BitmapImage Bitmap(string uri)
    {
        return new BitmapImage(new Uri(uri, UriKind.RelativeOrAbsolute));
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private async Task OpenSettingsAsync()
    {
        var window = new SettingsWindow(_settings, GetCaptureRootDirectory())
        {
            Owner = this
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        _settings = window.Settings;
        ApplySettingsToUi();
        await SaveSettingsAsync();
        LoadCaptures();
        SetStatus("Settings saved.");
    }

    private static string BuildComputedCss(SelectorInspectionResult inspection)
    {
        return string.Join(
            Environment.NewLine,
            inspection.ComputedStyle
                .OrderBy(item => item.Key)
                .Select(item => $"{item.Key}: {item.Value};"));
    }

    private void AddHistory(string entry)
    {
        _history.Insert(0, entry);
        if (_history.Count > _settings.History.MaxEntries)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        HistoryListBox.ItemsSource = null;
        HistoryListBox.ItemsSource = _history;
    }

    private void LoadCaptures()
    {
        var selectedDirectory = CaptureFilesListBox.SelectedItem is CaptureFileRecord selected
            ? selected.DirectoryPath
            : null;
        _captureFiles.Clear();
        var root = GetCaptureRootDirectory();
        Directory.CreateDirectory(root);

        foreach (var directory in Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetCreationTimeUtc))
        {
            _captureFiles.Add(CaptureFileRecord.FromDirectory(directory));
        }

        RefreshDynamicCaptureFilters();
        ApplyCaptureFilters(selectedDirectory);
    }

    private void ApplyCaptureFilters(string? preferredSelection = null)
    {
        if (CaptureFilesListBox is null
            || CaptureSearchTextBox is null
            || CaptureBrowserFilterComboBox is null
            || CaptureStatusFilterComboBox is null
            || CaptureSeverityFilterComboBox is null
            || CaptureDateFilterComboBox is null)
        {
            return;
        }

        var query = CaptureSearchTextBox?.Text?.Trim() ?? string.Empty;
        var browser = GetFilterValue(CaptureBrowserFilterComboBox);
        var status = GetFilterValue(CaptureStatusFilterComboBox);
        var severity = GetFilterValue(CaptureSeverityFilterComboBox);
        var dateRange = GetFilterValue(CaptureDateFilterComboBox);

        var filtered = _captureFiles
            .Where(capture => MatchesQuery(capture, query))
            .Where(capture => string.IsNullOrWhiteSpace(browser) || capture.Browser.Equals(browser, StringComparison.OrdinalIgnoreCase))
            .Where(capture => string.IsNullOrWhiteSpace(status) || capture.NoteStatus.Equals(status, StringComparison.OrdinalIgnoreCase))
            .Where(capture => string.IsNullOrWhiteSpace(severity) || capture.NoteSeverity.Equals(severity, StringComparison.OrdinalIgnoreCase))
            .Where(capture => MatchesDateRange(capture, dateRange))
            .OrderByDescending(capture => capture.CreatedAt)
            .ToArray();

        _filteredCaptureFiles.Clear();
        _filteredCaptureFiles.AddRange(filtered);
        CaptureFilesListBox.ItemsSource = null;
        CaptureFilesListBox.ItemsSource = _filteredCaptureFiles;

        if (!string.IsNullOrWhiteSpace(preferredSelection))
        {
            SelectCapture(preferredSelection);
            if (CaptureFilesListBox.SelectedItem is null && _filteredCaptureFiles.Count > 0)
            {
                CaptureFilesListBox.SelectedIndex = 0;
            }
        }
        else if (_filteredCaptureFiles.Count > 0 && CaptureFilesListBox.SelectedItem is null)
        {
            CaptureFilesListBox.SelectedIndex = 0;
        }

        SetCaptureFilterStatus();
        UpdateCaptureNotesPreview();
    }

    private void SelectCapture(string directory)
    {
        CaptureFilesListBox.SelectedItem = _filteredCaptureFiles.FirstOrDefault(item =>
            string.Equals(item.DirectoryPath, directory, StringComparison.OrdinalIgnoreCase));
        UpdateCaptureNotesPreview();
    }

    private void InitializeCaptureFilters()
    {
        CaptureBrowserFilterComboBox.ItemsSource = new[] { "All browsers" };
        CaptureStatusFilterComboBox.ItemsSource = new[] { "All statuses", "Open", "Needs review", "Confirmed", "Fixed", "Won't fix" };
        CaptureSeverityFilterComboBox.ItemsSource = new[] { "All severities", "Low", "Medium", "High", "Critical" };
        CaptureDateFilterComboBox.ItemsSource = new[] { "Any date", "Today", "Last 7 days", "Last 30 days" };

        CaptureBrowserFilterComboBox.SelectedIndex = 0;
        CaptureStatusFilterComboBox.SelectedIndex = 0;
        CaptureSeverityFilterComboBox.SelectedIndex = 0;
        CaptureDateFilterComboBox.SelectedIndex = 0;
    }

    private void RefreshDynamicCaptureFilters()
    {
        var selectedBrowser = CaptureBrowserFilterComboBox.SelectedItem as string;
        var browsers = new[] { "All browsers" }
            .Concat(_captureFiles
                .Select(capture => capture.Browser)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value))
            .ToArray();

        CaptureBrowserFilterComboBox.ItemsSource = browsers;
        CaptureBrowserFilterComboBox.SelectedItem = browsers.Contains(selectedBrowser, StringComparer.OrdinalIgnoreCase)
            ? selectedBrowser
            : "All browsers";
    }

    private void ClearCaptureFilters()
    {
        CaptureSearchTextBox.Text = string.Empty;
        CaptureBrowserFilterComboBox.SelectedIndex = 0;
        CaptureStatusFilterComboBox.SelectedIndex = 0;
        CaptureSeverityFilterComboBox.SelectedIndex = 0;
        CaptureDateFilterComboBox.SelectedIndex = 0;
        ApplyCaptureFilters();
    }

    private void SetCaptureFilterStatus()
    {
        var total = _captureFiles.Count;
        var visible = _filteredCaptureFiles.Count;
        if (total == visible)
        {
            SetStatus($"{total} capture(s) loaded.");
            return;
        }

        SetStatus($"{visible} of {total} capture(s) match the current filters.");
    }

    private static string GetFilterValue(System.Windows.Controls.ComboBox comboBox)
    {
        var value = comboBox.SelectedItem as string ?? string.Empty;
        return value.StartsWith("All ", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Any date", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value;
    }

    private static bool MatchesQuery(CaptureFileRecord capture, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(term => capture.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesDateRange(CaptureFileRecord capture, string dateRange)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return true;
        }

        var now = DateTimeOffset.Now;
        return dateRange switch
        {
            "Today" => capture.CreatedAt.LocalDateTime.Date == now.LocalDateTime.Date,
            "Last 7 days" => capture.CreatedAt >= now.AddDays(-7),
            "Last 30 days" => capture.CreatedAt >= now.AddDays(-30),
            _ => true
        };
    }

    private void UpdateCaptureNotesPreview()
    {
        if (CaptureFilesListBox.SelectedItem is not CaptureFileRecord capture)
        {
            CaptureNotesPreviewTextBlock.Text = "No capture selected.";
            return;
        }

        var notes = LoadCaptureNotes(capture.DirectoryPath);
        CaptureNotesPreviewTextBlock.Text = notes.ShortSummary;
    }

    private void OpenSelectedCapture()
    {
        if (CaptureFilesListBox.SelectedItem is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture first.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = capture.DirectoryPath,
            UseShellExecute = true
        });
    }

    private void RenameSelectedCapture()
    {
        if (CaptureFilesListBox.SelectedItem is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture first.");
            return;
        }

        var currentName = Path.GetFileName(capture.DirectoryPath);
        var requestedName = Microsoft.VisualBasic.Interaction.InputBox(
            "Capture folder name:",
            "Rename capture",
            currentName);

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return;
        }

        var safeName = SanitizeFileName(requestedName);
        var parent = Directory.GetParent(capture.DirectoryPath)?.FullName;
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        var destination = Path.Combine(parent, safeName);
        if (Directory.Exists(destination))
        {
            SetStatus("A capture with that name already exists.");
            return;
        }

        Directory.Move(capture.DirectoryPath, destination);
        LoadCaptures();
        SelectCapture(destination);
        SetStatus("Capture renamed.");
    }

    private void DeleteSelectedCapture()
    {
        if (CaptureFilesListBox.SelectedItem is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture first.");
            return;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            $"Delete capture '{capture.DisplayName}'?",
            "Delete capture",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Directory.Delete(capture.DirectoryPath, recursive: true);
        LoadCaptures();
        SetStatus("Capture deleted.");
    }

    private void EditSelectedEvidenceNotes()
    {
        if (CaptureFilesListBox.SelectedItem is not CaptureFileRecord capture)
        {
            SetStatus("Select an evidence package first.");
            return;
        }

        var evidencePath = Path.Combine(capture.DirectoryPath, "evidence.json");
        var summaryPath = Path.Combine(capture.DirectoryPath, "evidence-summary.md");
        var notes = LoadCaptureNotes(capture.DirectoryPath);
        var updatedNotes = PromptForEvidenceNotes(notes);

        SaveCaptureNotes(capture.DirectoryPath, updatedNotes);

        if (File.Exists(evidencePath))
        {
            try
            {
                var evidence = JsonSerializer.Deserialize<EvidencePackage>(File.ReadAllText(evidencePath));
                if (evidence is not null)
                {
                    evidence = evidence with
                    {
                        Notes = updatedNotes.ToEvidenceText(),
                        StructuredNotes = updatedNotes
                    };
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, jsonOptions));
                    File.WriteAllText(summaryPath, BuildEvidenceMarkdown(evidence));
                }
            }
            catch (JsonException)
            {
                SetStatus("Notes saved, but evidence.json could not be updated.");
                return;
            }
        }

        LoadCaptures();
        SelectCapture(capture.DirectoryPath);
        UpdateCaptureNotesPreview();
        SetStatus("Evidence notes saved.");
    }

    private void CompareCaptures()
    {
        var comparisonSource = _filteredCaptureFiles.Count > 0 ? _filteredCaptureFiles : _captureFiles;
        if (comparisonSource.Count < 2)
        {
            SetStatus("Create at least two captures or clear filters before comparing.");
            return;
        }

        var selectedDirectory = CaptureFilesListBox.SelectedItem is CaptureFileRecord capture
            ? capture.DirectoryPath
            : comparisonSource[0].DirectoryPath;
        var window = new CaptureComparisonWindow(
            comparisonSource.Select(item => item.DirectoryPath),
            selectedDirectory)
        {
            Owner = this,
            Topmost = _settings.Ui.KeepResultWindowsTopmost
        };

        PlaceResultWindow(window);
        window.Show();
        SetStatus("Capture comparison opened.");
    }

    private void ExportSelectedCaptureReport()
    {
        if (CaptureFilesListBox.SelectedItem is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture before exporting a report.");
            return;
        }

        try
        {
            SetBusy(true, "Building polished capture report...");
            var report = CaptureReport.FromDirectory(capture.DirectoryPath, GetActiveUsageProfile().DisplayName);
            var reportDirectory = Path.Combine(capture.DirectoryPath, "report");
            Directory.CreateDirectory(reportDirectory);

            var markdownPath = Path.Combine(reportDirectory, "report.md");
            var htmlPath = Path.Combine(reportDirectory, "report.html");
            var pdfPath = Path.Combine(reportDirectory, "report.pdf");

            File.WriteAllText(markdownPath, report.BuildMarkdown(), Encoding.UTF8);
            File.WriteAllText(htmlPath, report.BuildHtml(), Encoding.UTF8);
            SimplePdfReportWriter.Write(pdfPath, report);

            Process.Start(new ProcessStartInfo
            {
                FileName = reportDirectory,
                UseShellExecute = true
            });
            SetStatus($"Report exported: {reportDirectory}");
        }
        catch (Exception exception)
        {
            SetStatus($"Report export failed: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        LaunchChromeButton.IsEnabled = !isBusy;
        LaunchEdgeButton.IsEnabled = !isBusy;
        LaunchOperaButton.IsEnabled = !isBusy;
        LaunchFirefoxButton.IsEnabled = !isBusy;
        RefreshTargetsButton.IsEnabled = !isBusy;
        LensButton.IsEnabled = !isBusy;
        HelpButton.IsEnabled = !isBusy;
        SettingsButton.IsEnabled = !isBusy;
        InspectButton.IsEnabled = !isBusy;
        CaptureLensButton.IsEnabled = !isBusy;
        OpenCaptureButton.IsEnabled = !isBusy;
        RenameCaptureButton.IsEnabled = !isBusy;
        DeleteCaptureButton.IsEnabled = !isBusy;
        RefreshCapturesButton.IsEnabled = !isBusy;
        EditEvidenceNotesButton.IsEnabled = !isBusy;
        CompareCapturesButton.IsEnabled = !isBusy;
        ExportReportButton.IsEnabled = !isBusy;
        CopyHtmlButton.IsEnabled = !isBusy;
        CopyCssButton.IsEnabled = !isBusy;
        ExportJsonButton.IsEnabled = !isBusy;
        ShowImagesButton.IsEnabled = !isBusy;
        ShowIssuesButton.IsEnabled = !isBusy;

        if (message is not null)
        {
            SetStatus(message);
        }
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void OpenHelp()
    {
        var window = new HelpWindow
        {
            Owner = this
        };

        window.ShowDialog();
    }

    private string GetCaptureRootDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.Capture.ScreenshotDirectory))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(_settings.Capture.ScreenshotDirectory));
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Julco", "Captures");
    }

    private string BuildCaptureFolderName(CdpTarget target, SelectorInspectionResult inspection)
    {
        var value = _settings.Capture.FileNamePattern;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = CaptureSettings.Default.FileNamePattern;
        }

        value = value
            .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", DateTime.Now.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{browser}", _activeBrowser?.ToString() ?? "browser", StringComparison.OrdinalIgnoreCase)
            .Replace("{tag}", inspection.TagName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
            .Replace("{selector}", inspection.Selector, StringComparison.OrdinalIgnoreCase)
            .Replace("{title}", target.Title, StringComparison.OrdinalIgnoreCase);

        return SanitizeFileName(value);
    }

    private static string UniqueDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return path;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{path}-{index}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character =>
            invalid.Contains(character) ? '-' : character).ToArray());

        sanitized = sanitized.Trim('.', ' ', '-');
        return string.IsNullOrWhiteSpace(sanitized)
            ? $"capture-{DateTime.Now:yyyyMMdd-HHmmss}"
            : sanitized;
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Julco",
            "settings.json");
    }

    private enum BrowserKind
    {
        Chrome,
        Edge,
        Opera,
        Firefox
    }

    private sealed record UsageProfileDefinition(
        UsageProfile Profile,
        string DisplayName,
        string InspectorHelp,
        string Guidance,
        IReadOnlyList<string> TabPriority,
        IReadOnlyList<string> PrimaryActions);

    private sealed record CaptureManifest(
        DateTimeOffset CreatedAt,
        string PageTitle,
        string Url,
        string TagName,
        string Selector,
        double X,
        double Y,
        double Width,
        double Height,
        string Screenshot,
        string Inspection);

    private sealed record EvidencePackage(
        string Version,
        DateTimeOffset CreatedAt,
        EvidenceBrowserContext Browser,
        EvidencePageContext Page,
        EvidenceElementContext Element,
        EvidenceFrameContext Frame,
        EvidenceFiles Files,
        string Notes,
        CaptureNotes? StructuredNotes);

    private sealed record EvidenceBrowserContext(
        string Name,
        string TargetType,
        string RemotePort,
        string TargetId);

    private sealed record EvidencePageContext(
        string Title,
        string Url);

    private sealed record EvidenceElementContext(
        string TagName,
        string Selector,
        IReadOnlyDictionary<string, string> Attributes,
        int ImageResourceCount,
        int ConsoleMessageCount);

    private sealed record EvidenceFrameContext(
        double X,
        double Y,
        double Width,
        double Height,
        double CenterX,
        double CenterY,
        string ScreenName,
        int ScreenWidth,
        int ScreenHeight);

    private sealed record EvidenceFiles(
        string Screenshot,
        string Inspection,
        string Dom,
        string ComputedCss,
        string Console,
        string Attributes,
        string Images,
        string StructuredNotes,
        string Notes,
        string Summary);

    private sealed record CaptureReport(
        string CaptureDirectory,
        string Title,
        DateTimeOffset CreatedAt,
        string Browser,
        string RemotePort,
        string TargetType,
        string PageUrl,
        string PageTitle,
        string UsageProfile,
        string TagName,
        string Selector,
        EvidenceFrameContext Frame,
        CaptureNotes Notes,
        string ScreenshotPath,
        string Dom,
        string ComputedCss,
        string Console,
        string Attributes,
        string CommonIssues,
        IReadOnlyList<WebImageResource> Images)
    {
        public static CaptureReport FromDirectory(string captureDirectory, string usageProfile)
        {
            var evidence = LoadJsonFile<EvidencePackage>(Path.Combine(captureDirectory, "evidence.json"));
            var manifest = evidence is null
                ? LoadJsonFile<CaptureManifest>(Path.Combine(captureDirectory, "manifest.json"))
                : null;
            var notes = evidence?.StructuredNotes ?? LoadCaptureNotes(captureDirectory);
            var screenshot = ResolvePath(captureDirectory, evidence?.Files.Screenshot ?? manifest?.Screenshot ?? "screenshot.png");
            var createdAt = evidence?.CreatedAt
                ?? manifest?.CreatedAt
                ?? new DateTimeOffset(Directory.GetCreationTime(captureDirectory));
            var frame = evidence?.Frame ?? new EvidenceFrameContext(
                manifest?.X ?? 0,
                manifest?.Y ?? 0,
                manifest?.Width ?? 0,
                manifest?.Height ?? 0,
                (manifest?.X ?? 0) + ((manifest?.Width ?? 0) / 2),
                (manifest?.Y ?? 0) + ((manifest?.Height ?? 0) / 2),
                "Unknown",
                0,
                0);
            var images = LoadJsonFile<WebImageResource[]>(
                    ResolvePath(captureDirectory, evidence?.Files.Images ?? "image-resources.json"))
                ?? Array.Empty<WebImageResource>();
            var title = evidence?.Page.Title
                ?? manifest?.PageTitle
                ?? Path.GetFileName(captureDirectory);

            return new CaptureReport(
                captureDirectory,
                string.IsNullOrWhiteSpace(title) ? "Julco Capture Report" : title,
                createdAt,
                evidence?.Browser.Name ?? "Unknown",
                evidence?.Browser.RemotePort ?? "-",
                evidence?.Browser.TargetType ?? "-",
                evidence?.Page.Url ?? manifest?.Url ?? "-",
                title,
                usageProfile,
                evidence?.Element.TagName ?? manifest?.TagName ?? "-",
                evidence?.Element.Selector ?? manifest?.Selector ?? "-",
                frame,
                notes,
                File.Exists(screenshot) ? screenshot : string.Empty,
                ReadText(ResolvePath(captureDirectory, evidence?.Files.Dom ?? "dom.html")),
                ReadText(ResolvePath(captureDirectory, evidence?.Files.ComputedCss ?? "computed.css")),
                ReadText(ResolvePath(captureDirectory, evidence?.Files.Console ?? "console.txt")),
                ReadText(ResolvePath(captureDirectory, evidence?.Files.Attributes ?? "attributes.txt")),
                ReadText(Path.Combine(captureDirectory, "common-issues.md")),
                images);
        }

        public string BuildMarkdown()
        {
            var lines = new List<string>
            {
                "# Julco Capture Report",
                string.Empty,
                $"**Page:** {NormalizeMarkdownLine(PageTitle)}",
                $"**URL:** {NormalizeMarkdownLine(PageUrl)}",
                $"**Created:** {CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
                $"**Profile:** {NormalizeMarkdownLine(UsageProfile)}",
                string.Empty
            };

            if (!string.IsNullOrWhiteSpace(ScreenshotPath))
            {
                lines.Add("![Capture screenshot](../screenshot.png)");
                lines.Add(string.Empty);
            }

            lines.AddRange(new[]
            {
                "## Technical Summary",
                string.Empty,
                "| Field | Value |",
                "| --- | --- |",
                $"| Browser | {NormalizeMarkdownLine(Browser)} |",
                $"| Remote port | {NormalizeMarkdownLine(RemotePort)} |",
                $"| Target type | {NormalizeMarkdownLine(TargetType)} |",
                $"| Profile | {NormalizeMarkdownLine(UsageProfile)} |",
                $"| Element | `{NormalizeMarkdownLine(TagName)}` |",
                $"| Selector | `{NormalizeMarkdownLine(Selector)}` |",
                $"| Lens frame | {Frame.Width:0}x{Frame.Height:0} at {Frame.X:0},{Frame.Y:0} |",
                $"| Center | {Frame.CenterX:0},{Frame.CenterY:0} |",
                $"| Screen | {NormalizeMarkdownLine(Frame.ScreenName)} {Frame.ScreenWidth}x{Frame.ScreenHeight} |",
                $"| Image resources | {Images.Count} |",
                string.Empty,
                "## Notes",
                string.Empty,
                Notes.HasContent ? Notes.ToMarkdown() : "_No notes added._",
                string.Empty,
                "## Common Issues",
                string.Empty,
                string.IsNullOrWhiteSpace(CommonIssues) ? "_No issue report found._" : CommonIssues,
                string.Empty,
                "## Attributes",
                string.Empty,
                CodeBlock(Attributes, "text"),
                string.Empty,
                "## Computed CSS",
                string.Empty,
                CodeBlock(ComputedCss, "css"),
                string.Empty,
                "## DOM",
                string.Empty,
                CodeBlock(Dom, "html")
            });

            return string.Join(Environment.NewLine, lines);
        }

        public string BuildHtml()
        {
            var screenshotMarkup = string.IsNullOrWhiteSpace(ScreenshotPath)
                ? "<div class=\"empty\">No screenshot found.</div>"
                : "<img class=\"screenshot\" src=\"../screenshot.png\" alt=\"Capture screenshot\" />";
            var notesMarkup = Notes.HasContent
                ? $"<dl>{Definition("Category", Notes.Category)}{Definition("Severity", Notes.Severity)}{Definition("Status", Notes.Status)}{Definition("Tags", Notes.Tags)}</dl><p>{EscapeHtml(Notes.Observation)}</p>"
                : "<p class=\"empty\">No notes added.</p>";
            var imageRows = Images.Count == 0
                ? "<tr><td colspan=\"5\">No image resources detected.</td></tr>"
                : string.Join(Environment.NewLine, Images.Take(80).Select(image =>
                    $"<tr><td>{EscapeHtml(image.Kind)}</td><td>{EscapeHtml(image.Format)}</td><td>{image.DisplayedWidth}x{image.DisplayedHeight}</td><td>{image.NaturalWidth}x{image.NaturalHeight}</td><td><code>{EscapeHtml(Shorten(image.Url, 120))}</code></td></tr>"));

            return $$"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1" />
                  <title>Julco Capture Report</title>
                  <style>
                    :root { color-scheme: dark; --bg:#0f1218; --panel:#181d26; --ink:#f5f7fb; --muted:#aeb8c8; --line:#2d3645; --accent:#41c7ff; --soft:#111722; }
                    * { box-sizing: border-box; }
                    body { margin:0; background:var(--bg); color:var(--ink); font:14px/1.55 "Segoe UI", Arial, sans-serif; }
                    main { max-width:1120px; margin:0 auto; padding:32px; }
                    header { border-bottom:1px solid var(--line); padding-bottom:18px; margin-bottom:24px; }
                    h1 { margin:0 0 6px; font-size:30px; letter-spacing:0; }
                    h2 { margin:0 0 12px; font-size:18px; color:var(--accent); }
                    .muted { color:var(--muted); }
                    .hero { display:grid; grid-template-columns:minmax(280px, 1.2fr) minmax(260px, .8fr); gap:18px; align-items:start; }
                    .panel { background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:18px; margin-bottom:18px; }
                    .screenshot { width:100%; max-height:520px; object-fit:contain; background:#05070a; border:1px solid var(--line); border-radius:6px; }
                    dl { display:grid; grid-template-columns:130px 1fr; gap:8px 14px; margin:0; }
                    dt { color:var(--muted); }
                    dd { margin:0; word-break:break-word; }
                    code, pre { font-family:Consolas, "Cascadia Mono", monospace; }
                    pre { max-height:360px; overflow:auto; padding:14px; border-radius:6px; background:#05070a; border:1px solid var(--line); white-space:pre-wrap; }
                    table { width:100%; border-collapse:collapse; }
                    th, td { text-align:left; border-bottom:1px solid var(--line); padding:8px; vertical-align:top; }
                    th { color:var(--accent); font-weight:600; }
                    .empty { color:var(--muted); padding:18px; border:1px dashed var(--line); border-radius:6px; }
                    @media (max-width: 760px) { main { padding:18px; } .hero { grid-template-columns:1fr; } dl { grid-template-columns:1fr; } }
                  </style>
                </head>
                <body>
                <main>
                  <header>
                    <h1>Julco Capture Report</h1>
                    <div class="muted">{{EscapeHtml(PageTitle)}} · {{EscapeHtml(CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"))}}</div>
                  </header>
                  <section class="hero">
                    <div class="panel">{{screenshotMarkup}}</div>
                    <div class="panel">
                      <h2>Technical Summary</h2>
                      <dl>
                        {{Definition("URL", PageUrl)}}
                        {{Definition("Browser", Browser)}}
                        {{Definition("Remote port", RemotePort)}}
                        {{Definition("Target type", TargetType)}}
                        {{Definition("Profile", UsageProfile)}}
                        {{Definition("Element", TagName)}}
                        {{Definition("Selector", Selector)}}
                        {{Definition("Lens frame", $"{Frame.Width:0}x{Frame.Height:0} at {Frame.X:0},{Frame.Y:0}")}}
                        {{Definition("Center", $"{Frame.CenterX:0},{Frame.CenterY:0}")}}
                        {{Definition("Screen", $"{Frame.ScreenName} {Frame.ScreenWidth}x{Frame.ScreenHeight}")}}
                        {{Definition("Images", Images.Count.ToString(CultureInfo.InvariantCulture))}}
                      </dl>
                    </div>
                  </section>
                  <section class="panel"><h2>Notes</h2>{{notesMarkup}}</section>
                  <section class="panel"><h2>Common Issues</h2><pre>{{EscapeHtml(string.IsNullOrWhiteSpace(CommonIssues) ? "No issue report found." : CommonIssues)}}</pre></section>
                  <section class="panel"><h2>Image Resources</h2><table><thead><tr><th>Kind</th><th>Format</th><th>Shown</th><th>Natural</th><th>URL</th></tr></thead><tbody>{{imageRows}}</tbody></table></section>
                  <section class="panel"><h2>Attributes</h2><pre>{{EscapeHtml(Attributes)}}</pre></section>
                  <section class="panel"><h2>Computed CSS</h2><pre>{{EscapeHtml(ComputedCss)}}</pre></section>
                  <section class="panel"><h2>DOM</h2><pre>{{EscapeHtml(Dom)}}</pre></section>
                </main>
                </body>
                </html>
                """;
        }

        public IReadOnlyList<string> BuildPdfLines()
        {
            var lines = new List<string>
            {
                "Julco Capture Report",
                $"Page: {PageTitle}",
                $"URL: {PageUrl}",
                $"Created: {CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
                $"Profile: {UsageProfile}",
                $"Browser: {Browser} | Port: {RemotePort} | Target: {TargetType}",
                $"Element: {TagName} | Selector: {Selector}",
                $"Lens: {Frame.Width:0}x{Frame.Height:0} at {Frame.X:0},{Frame.Y:0} | Center: {Frame.CenterX:0},{Frame.CenterY:0}",
                $"Screen: {Frame.ScreenName} {Frame.ScreenWidth}x{Frame.ScreenHeight}",
                string.Empty,
                "Notes",
                Notes.HasContent ? Notes.ShortSummary : "No notes added.",
                string.IsNullOrWhiteSpace(Notes.Observation) ? string.Empty : Notes.Observation,
                string.Empty,
                "Common Issues",
                string.IsNullOrWhiteSpace(CommonIssues) ? "No issue report found." : CommonIssues,
                string.Empty,
                "Attributes",
                Attributes,
                string.Empty,
                "Computed CSS",
                ComputedCss,
                string.Empty,
                "DOM preview",
                Shorten(Dom, 5000)
            };

            return lines
                .SelectMany(line => WrapLine(line.ReplaceLineEndings(" "), 96))
                .ToArray();
        }

        private static T? LoadJsonFile<T>(string path)
        {
            if (!File.Exists(path))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return default;
            }
        }

        private static string ResolvePath(string captureDirectory, string? relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            {
                return captureDirectory;
            }

            return Path.IsPathRooted(relativeOrAbsolute)
                ? relativeOrAbsolute
                : Path.Combine(captureDirectory, relativeOrAbsolute);
        }

        private static string ReadText(string path)
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static string CodeBlock(string content, string language)
        {
            return $"```{language}{Environment.NewLine}{content.Trim()}{Environment.NewLine}```";
        }

        private static string Definition(string key, string? value)
        {
            return $"<dt>{EscapeHtml(key)}</dt><dd>{EscapeHtml(string.IsNullOrWhiteSpace(value) ? "-" : value)}</dd>";
        }

        private static string EscapeHtml(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string Shorten(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value[..Math.Max(0, maxLength - 1)] + "...";
        }

        private static IEnumerable<string> WrapLine(string line, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                yield return string.Empty;
                yield break;
            }

            var remaining = line.Trim();
            while (remaining.Length > maxLength)
            {
                var splitAt = remaining.LastIndexOf(' ', Math.Min(maxLength, remaining.Length - 1));
                if (splitAt < maxLength / 2)
                {
                    splitAt = maxLength;
                }

                yield return remaining[..splitAt].Trim();
                remaining = remaining[splitAt..].Trim();
            }

            if (remaining.Length > 0)
            {
                yield return remaining;
            }
        }
    }

    private static class SimplePdfReportWriter
    {
        public static void Write(string path, CaptureReport report)
        {
            var image = PdfImage.TryLoad(report.ScreenshotPath);
            var lines = report.BuildPdfLines();
            var pages = lines.Chunk(42).ToArray();
            if (pages.Length == 0)
            {
                pages = new[] { Array.Empty<string>() };
            }

            var hasImage = image is not null;
            var pageCount = pages.Length;
            var fontId = 3;
            var imageId = hasImage ? 4 : 0;
            var firstPageId = hasImage ? 5 : 4;
            var objectCount = firstPageId + (pageCount * 2) - 1;
            var pageIds = Enumerable.Range(0, pageCount)
                .Select(index => firstPageId + (index * 2))
                .ToArray();

            var objects = new byte[objectCount + 1][];
            objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
            objects[2] = Ascii($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageCount} >>");
            objects[3] = Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            if (hasImage)
            {
                objects[imageId] = BuildImageObject(image!);
            }

            for (var index = 0; index < pageCount; index++)
            {
                var pageId = pageIds[index];
                var contentId = pageId + 1;
                var resources = hasImage
                    ? $"<< /Font << /F1 {fontId} 0 R >> /XObject << /Im1 {imageId} 0 R >> >>"
                    : $"<< /Font << /F1 {fontId} 0 R >> >>";
                objects[pageId] = Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources {resources} /Contents {contentId} 0 R >>");
                objects[contentId] = BuildContentObject(pages[index], image, index == 0);
            }

            using var stream = File.Create(path);
            WriteAscii(stream, "%PDF-1.4\n%Julco\n");
            var offsets = new long[objects.Length];
            for (var id = 1; id < objects.Length; id++)
            {
                offsets[id] = stream.Position;
                WriteAscii(stream, $"{id} 0 obj\n");
                stream.Write(objects[id]);
                WriteAscii(stream, "\nendobj\n");
            }

            var xref = stream.Position;
            WriteAscii(stream, $"xref\n0 {objects.Length}\n0000000000 65535 f \n");
            for (var id = 1; id < objects.Length; id++)
            {
                WriteAscii(stream, $"{offsets[id]:0000000000} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {objects.Length} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        }

        private static byte[] BuildContentObject(string[] lines, PdfImage? image, bool includeImage)
        {
            using var content = new MemoryStream();
            if (includeImage && image is not null)
            {
                var maxWidth = 470.0;
                var maxHeight = 245.0;
                var scale = Math.Min(maxWidth / image.Width, maxHeight / image.Height);
                var width = image.Width * scale;
                var height = image.Height * scale;
                var x = (612 - width) / 2;
                var y = 792 - height - 42;
                WriteAscii(content, FormattableString.Invariant($"q {width:0.##} 0 0 {height:0.##} {x:0.##} {y:0.##} cm /Im1 Do Q\n"));
            }

            var startY = includeImage && image is not null ? 480 : 742;
            WriteAscii(content, "BT /F1 10 Tf 46 ");
            WriteAscii(content, startY.ToString(CultureInfo.InvariantCulture));
            WriteAscii(content, " Td 14 TL\n");
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    WriteAscii(content, "T*\n");
                    continue;
                }

                WriteAscii(content, $"{ToPdfHexString(line)} Tj T*\n");
            }

            WriteAscii(content, "ET\n");
            return WrapStream(content.ToArray());
        }

        private static byte[] BuildImageObject(PdfImage image)
        {
            var header = Ascii($"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {image.Rgb.Length} >>\nstream\n");
            var footer = Ascii("\nendstream");
            return Combine(header, image.Rgb, footer);
        }

        private static byte[] WrapStream(byte[] content)
        {
            return Combine(
                Ascii($"<< /Length {content.Length} >>\nstream\n"),
                content,
                Ascii("\nendstream"));
        }

        private static string ToPdfHexString(string value)
        {
            var bytes = Encoding.BigEndianUnicode.GetBytes(value);
            var builder = new StringBuilder("<FEFF", 4 + (bytes.Length * 2) + 1);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            builder.Append('>');
            return builder.ToString();
        }

        private static byte[] Combine(params byte[][] parts)
        {
            var length = parts.Sum(part => part.Length);
            var result = new byte[length];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }

        private static byte[] Ascii(string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Ascii(value);
            stream.Write(bytes);
        }

        private sealed record PdfImage(int Width, int Height, byte[] Rgb)
        {
            public static PdfImage? TryLoad(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return null;
                }

                try
                {
                    using var source = new System.Drawing.Bitmap(path);
                    using var bitmap = new System.Drawing.Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.White);
                        graphics.DrawImage(source, 0, 0, source.Width, source.Height);
                    }

                    var bytes = new byte[bitmap.Width * bitmap.Height * 3];
                    var offset = 0;
                    for (var y = 0; y < bitmap.Height; y++)
                    {
                        for (var x = 0; x < bitmap.Width; x++)
                        {
                            var pixel = bitmap.GetPixel(x, y);
                            bytes[offset++] = pixel.R;
                            bytes[offset++] = pixel.G;
                            bytes[offset++] = pixel.B;
                        }
                    }

                    return new PdfImage(bitmap.Width, bitmap.Height, bytes);
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    private sealed record CaptureFileRecord(
        string DirectoryPath,
        string DisplayName,
        DateTimeOffset CreatedAt,
        string Browser,
        string Url,
        string PageTitle,
        string TagName,
        string Selector,
        string NoteStatus,
        string NoteSeverity,
        string NoteTags,
        string NoteText,
        string SearchText)
    {
        public static CaptureFileRecord FromDirectory(string directoryPath)
        {
            var evidencePath = Path.Combine(directoryPath, "evidence.json");
            if (File.Exists(evidencePath))
            {
                try
                {
                    var evidence = JsonSerializer.Deserialize<EvidencePackage>(File.ReadAllText(evidencePath));
                    if (evidence is not null)
                    {
                        var notes = evidence.StructuredNotes ?? LoadCaptureNotes(directoryPath);
                        var noteMarker = notes.HasContent
                            ? $"  notes:{notes.Severity}/{notes.Status}"
                            : string.Empty;
                        var displayName = $"{evidence.CreatedAt:MM-dd HH:mm}  evidence  {evidence.Element.TagName}  {evidence.Element.Selector}{noteMarker}";
                        return new CaptureFileRecord(
                            directoryPath,
                            displayName,
                            evidence.CreatedAt,
                            evidence.Browser.Name,
                            evidence.Page.Url,
                            evidence.Page.Title,
                            evidence.Element.TagName,
                            evidence.Element.Selector,
                            notes.Status,
                            notes.Severity,
                            notes.Tags,
                            notes.Observation,
                            BuildCaptureSearchText(
                                directoryPath,
                                displayName,
                                evidence.Browser.Name,
                                evidence.Page.Url,
                                evidence.Page.Title,
                                evidence.Element.TagName,
                                evidence.Element.Selector,
                                notes));
                    }
                }
                catch (JsonException)
                {
                }
            }

            var manifestPath = Path.Combine(directoryPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifest = JsonSerializer.Deserialize<CaptureManifest>(File.ReadAllText(manifestPath));
                    if (manifest is not null)
                    {
                        var notes = LoadCaptureNotes(directoryPath);
                        var noteMarker = notes.HasContent
                            ? $"  notes:{notes.Severity}/{notes.Status}"
                            : string.Empty;
                        var displayName = $"{manifest.CreatedAt:MM-dd HH:mm}  {manifest.TagName}  {manifest.Selector}{noteMarker}";
                        return new CaptureFileRecord(
                            directoryPath,
                            displayName,
                            manifest.CreatedAt,
                            "Unknown",
                            manifest.Url,
                            manifest.PageTitle,
                            manifest.TagName,
                            manifest.Selector,
                            notes.Status,
                            notes.Severity,
                            notes.Tags,
                            notes.Observation,
                            BuildCaptureSearchText(
                                directoryPath,
                                displayName,
                                "Unknown",
                                manifest.Url,
                                manifest.PageTitle,
                                manifest.TagName,
                                manifest.Selector,
                                notes));
                    }
                }
                catch (JsonException)
                {
                }
            }

            var fallbackNotes = LoadCaptureNotes(directoryPath);
            var fallbackName = Path.GetFileName(directoryPath);
            return new CaptureFileRecord(
                directoryPath,
                fallbackName,
                Directory.GetCreationTime(directoryPath),
                "Unknown",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                fallbackNotes.Status,
                fallbackNotes.Severity,
                fallbackNotes.Tags,
                fallbackNotes.Observation,
                BuildCaptureSearchText(
                    directoryPath,
                    fallbackName,
                    "Unknown",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    fallbackNotes));
        }

        private static string BuildCaptureSearchText(
            string directoryPath,
            string displayName,
            string browser,
            string url,
            string pageTitle,
            string tagName,
            string selector,
            CaptureNotes notes)
        {
            return string.Join(
                " ",
                displayName,
                browser,
                url,
                pageTitle,
                tagName,
                selector,
                notes.Category,
                notes.Severity,
                notes.Status,
                notes.Tags,
                notes.Observation,
                Path.GetFileName(directoryPath));
        }
    }
}
