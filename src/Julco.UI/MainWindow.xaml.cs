using System.IO;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
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
using Julco.Core.Privacy;
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
    private readonly GlobalHotkeyService _globalHotkeys = new();
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
    private bool _isSourceInitialized;
    private bool _isLensFrozen;
    private string _lastLensDetectedType = "-";
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
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
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

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _isSourceInitialized = true;
        RefreshGlobalHotkeys();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _globalHotkeys.Dispose();
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (TryHandleLocalShortcut(e))
        {
            e.Handled = true;
        }
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

    private void IssueTrackerButton_Click(object sender, RoutedEventArgs e) => GenerateIssueTrackerReports();

    private void PrivacyPreviewButton_Click(object sender, RoutedEventArgs e) => ShowPrivacyPreview();

    private void HealthButton_Click(object sender, RoutedEventArgs e) => ToggleHealthPanel();

    private void CloseHealthPanelButton_Click(object sender, RoutedEventArgs e)
    {
        HealthPanel.Visibility = Visibility.Collapsed;
    }

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

    private void CaptureFilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncSelectedCapture(CaptureFilesListBox.SelectedItem as CaptureFileRecord);
    }

    private void CaptureFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncSelectedCapture(CaptureFilesDataGrid.SelectedItem as CaptureFileRecord);
    }

    private void CaptureHistory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedCapture();
    }

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

        System.Windows.Clipboard.SetText(RedactExportHtml(_currentInspection.OuterHtml));
        SetStatus(_settings.Privacy.RedactOnExport ? "HTML copied with privacy redaction." : "HTML copied.");
    }

    private void CopyCssButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInspection is null)
        {
            SetStatus("No active inspection.");
            return;
        }

        System.Windows.Clipboard.SetText(RedactExportText(BuildComputedCss(_currentInspection)));
        SetStatus(_settings.Privacy.RedactOnExport ? "Computed CSS copied with privacy redaction." : "Computed CSS copied.");
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

        File.WriteAllText(dialog.FileName, RedactExportText(json));
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
        _lensWindow.FreezeChanged += LensWindow_FreezeChanged;
        _lensWindow.LockChanged += LensWindow_LockChanged;
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
            $"Center {e.State.CenterPoint.X:0},{e.State.CenterPoint.Y:0} | Frame {e.State.Bounds.Width:0}x{e.State.Bounds.Height:0} | {_lastLensDetectedType}";
        UpdateHealthPanel();
        ScheduleAutoLensInspection();
    }

    private async void LensWindow_InspectCenterRequested(object? sender, LensFrameState state) => await InspectLensCenterAsync(state, respectFreeze: false);

    private async void LensWindow_CaptureRequested(object? sender, LensFrameState state)
    {
        _lastLensState = state;
        await CaptureLensAsync();
    }

    private void LensWindow_FreezeChanged(object? sender, bool isFrozen)
    {
        _isLensFrozen = isFrozen;
        if (isFrozen)
        {
            _autoLensTimer.Stop();
            SetStatus("Lens frozen. Move/resize is still available, but live inspection is paused.");
            return;
        }

        SetStatus("Lens live inspection resumed.");
        ScheduleAutoLensInspection();
    }

    private void LensWindow_LockChanged(object? sender, bool isLocked)
    {
        SetStatus(isLocked
            ? "Lens locked. Movement and resizing are disabled."
            : "Lens unlocked. Movement and resizing are enabled.");
        UpdateHealthPanel();
    }

    private void LensWindow_Closed(object? sender, EventArgs e)
    {
        if (_lensWindow is not null)
        {
            _lensWindow.LensChanged -= LensWindow_LensChanged;
            _lensWindow.InspectCenterRequested -= LensWindow_InspectCenterRequested;
            _lensWindow.CaptureRequested -= LensWindow_CaptureRequested;
            _lensWindow.FreezeChanged -= LensWindow_FreezeChanged;
            _lensWindow.LockChanged -= LensWindow_LockChanged;
            _lensWindow.Closed -= LensWindow_Closed;
        }

        _lensWindow = null;
        _isLensFrozen = false;
        _lastLensDetectedType = "-";
        _lastLiveLensHistoryKey = null;
        LensButtonTextBlock.Text = "Lens";
        LensStateTextBlock.Text = "Inactive";
        _autoLensTimer.Stop();
        UpdateHealthPanel();
        SetStatus("Lens closed.");
    }

    private async Task InspectLensCenterAsync(LensFrameState state, bool respectFreeze = true)
    {
        if (_isInspectingLens)
        {
            return;
        }

        if (respectFreeze && _isLensFrozen)
        {
            SetStatus("Lens is frozen. Unfreeze to refresh live inspection.");
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
            _lastLensDetectedType = DetectLensContentType(result);
            _lensWindow?.SetDetectedType(_lastLensDetectedType);
            UpdateLensStateText(state);
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
            _lastLensDetectedType = DetectLensContentType(inspection);
            _lensWindow?.SetDetectedType(_lastLensDetectedType);
            UpdateLensStateText(state);

            var captureRoot = GetCaptureRootDirectory();
            Directory.CreateDirectory(captureRoot);

            var folderName = BuildCaptureFolderName(target, inspection);
            var captureDirectory = UniqueDirectory(Path.Combine(captureRoot, folderName));
            Directory.CreateDirectory(captureDirectory);

            var screenshotPath = Path.Combine(captureDirectory, "screenshot.png");
            var screenshotBytes = await CaptureRegionAsync(state, screenshotPath);
            _lastLensPreviewImage = CreateLensPreviewImage(state, screenshotBytes);
            var evidenceImages = BuildImagesWithLensPreview(inspection.Images);
            var commonIssues = CommonIssueDetector.Detect(inspection);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var sanitizedInspectionJson = RedactExportText(JsonSerializer.Serialize(inspection, jsonOptions));
            var sanitizedOuterHtml = RedactExportHtml(inspection.OuterHtml);
            var sanitizedComputedCss = RedactExportText(BuildComputedCss(inspection));
            var sanitizedConsole = RedactExportText(string.Join(Environment.NewLine, inspection.ConsoleMessages));
            var sanitizedAttributes = RedactExportText(string.Join(
                Environment.NewLine,
                inspection.Attributes.Select(item => $"{item.Key}=\"{item.Value}\"")));
            var sanitizedImagesJson = RedactExportText(JsonSerializer.Serialize(evidenceImages, jsonOptions));
            var sanitizedCommonIssues = RedactExportText(CommonIssueDetector.BuildReport(commonIssues));
            File.WriteAllText(
                Path.Combine(captureDirectory, "inspection.json"),
                sanitizedInspectionJson);
            File.WriteAllText(Path.Combine(captureDirectory, "dom.html"), sanitizedOuterHtml);
            File.WriteAllText(Path.Combine(captureDirectory, "computed.css"), sanitizedComputedCss);
            File.WriteAllText(Path.Combine(captureDirectory, "console.txt"), sanitizedConsole);
            File.WriteAllText(Path.Combine(captureDirectory, "attributes.txt"), sanitizedAttributes);
            File.WriteAllText(
                Path.Combine(captureDirectory, "image-resources.json"),
                sanitizedImagesJson);
            File.WriteAllText(
                Path.Combine(captureDirectory, "common-issues.json"),
                RedactExportText(JsonSerializer.Serialize(commonIssues, jsonOptions)));
            File.WriteAllText(
                Path.Combine(captureDirectory, "common-issues.md"),
                sanitizedCommonIssues);

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
                RedactExportText(JsonSerializer.Serialize(evidence, jsonOptions)));
            File.WriteAllText(
                Path.Combine(captureDirectory, "evidence-summary.md"),
                RedactExportText(BuildEvidenceMarkdown(evidence)));

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
                RedactExportText(JsonSerializer.Serialize(manifest, jsonOptions)));

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
                DetectLensContentType(inspection),
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
            $"- Detected type: {NormalizeMarkdownLine(evidence.Element.DetectedType)}",
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

    private PrivacyRedactorOptions GetPrivacyOptions()
    {
        return PrivacyRedactorOptions.FromSettings(_settings.Privacy ?? PrivacySettings.Default);
    }

    private string RedactExportText(string? value)
    {
        return PrivacyRedactor.RedactText(value, GetPrivacyOptions());
    }

    private string RedactExportHtml(string? value)
    {
        return PrivacyRedactor.RedactHtml(value, GetPrivacyOptions());
    }

    private CaptureNotes RedactCaptureNotes(CaptureNotes notes)
    {
        var options = GetPrivacyOptions();
        if (!options.Enabled)
        {
            return notes;
        }

        return notes with
        {
            Observation = PrivacyRedactor.RedactText(notes.Observation, options),
            Tags = PrivacyRedactor.RedactText(notes.Tags, options)
        };
    }

    private static string NormalizeMarkdownLine(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.ReplaceLineEndings(" ").Trim();
    }

    private static CaptureNotes LoadCaptureNotes(string captureDirectory)
    {
        return CaptureNotesStore.Load(captureDirectory);
    }

    private void SaveCaptureNotes(string captureDirectory, CaptureNotes notes)
    {
        var sanitizedNotes = RedactCaptureNotes(notes);
        CaptureNotesStore.Save(captureDirectory, sanitizedNotes);
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
        if (_lensWindow is null || _lastLensState is null || _isLensFrozen)
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

        var window = new ImageResourcesWindow(images, GetPrivacyOptions())
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

    private static string DetectLensContentType(SelectorInspectionResult inspection)
    {
        var tag = inspection.TagName.Trim().ToLowerInvariant();
        var role = GetAttributeValue(inspection.Attributes, "role").ToLowerInvariant();
        var type = GetAttributeValue(inspection.Attributes, "type").ToLowerInvariant();
        var display = GetStyleValue(inspection.ComputedStyle, "display").ToLowerInvariant();
        var visibility = GetStyleValue(inspection.ComputedStyle, "visibility").ToLowerInvariant();
        var opacity = GetStyleValue(inspection.ComputedStyle, "opacity");
        if (display == "none" || visibility == "hidden" || opacity == "0")
        {
            return "hidden";
        }

        if (tag is "img" or "picture" or "source" or "svg" or "canvas"
            || inspection.Images.Any(image => image.IsLensFrame is false && image.Kind.Contains("img", StringComparison.OrdinalIgnoreCase)))
        {
            return "image";
        }

        if (tag is "video" or "audio" or "iframe" or "embed" or "object")
        {
            return "media";
        }

        if (tag is "input" or "textarea" or "select" or "option" or "label"
            || role is "textbox" or "combobox" or "checkbox" or "radio" or "switch")
        {
            return string.IsNullOrWhiteSpace(type) ? "form" : $"form:{type}";
        }

        if (tag is "button" or "a" or "summary"
            || role is "button" or "link" or "menuitem" or "tab")
        {
            return "action";
        }

        if (tag is "p" or "span" or "strong" or "em" or "small" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
        {
            return "text";
        }

        if (display.Contains("flex", StringComparison.OrdinalIgnoreCase)
            || display.Contains("grid", StringComparison.OrdinalIgnoreCase))
        {
            return "layout";
        }

        return tag is "div" or "section" or "article" or "main" or "nav" or "header" or "footer"
            ? "container"
            : tag;
    }

    private void UpdateLensStateText(LensFrameState state)
    {
        LensStateTextBlock.Text =
            $"Center {state.CenterPoint.X:0},{state.CenterPoint.Y:0} | Frame {state.Bounds.Width:0}x{state.Bounds.Height:0} | {_lastLensDetectedType}";
    }

    private static string GetAttributeValue(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string GetStyleValue(IReadOnlyDictionary<string, string> styles, string key)
    {
        return styles.TryGetValue(key, out var value) ? value : string.Empty;
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

    private bool TryHandleLocalShortcut(System.Windows.Input.KeyEventArgs e)
    {
        if (!_settings.Keyboard.EnableLocalShortcuts)
        {
            return false;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var hotkey = BuildLocalHotkeys().FirstOrDefault(item =>
            item.Key == key && item.Modifiers == modifiers);
        if (hotkey is null)
        {
            return false;
        }

        RunHotkeyAction(hotkey);
        return true;
    }

    private void RunHotkeyAction(HotkeyDefinition hotkey)
    {
        try
        {
            hotkey.Action();
            SetStatus($"Shortcut: {hotkey.Name}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Shortcut failed ({hotkey.Name}): {exception.Message}");
        }
    }

    private IReadOnlyList<HotkeyDefinition> BuildGlobalHotkeys()
    {
        if (!_settings.Keyboard.EnableGlobalShortcuts)
        {
            return Array.Empty<HotkeyDefinition>();
        }

        return BuildConfiguredHotkeys(_settings.Keyboard.GlobalShortcuts, 1000, wrapAction: true);
    }

    private IReadOnlyList<HotkeyDefinition> BuildLocalHotkeys()
    {
        if (!_settings.Keyboard.EnableLocalShortcuts)
        {
            return Array.Empty<HotkeyDefinition>();
        }

        return BuildConfiguredHotkeys(_settings.Keyboard.LocalShortcuts, 2000, wrapAction: false);
    }

    private IReadOnlyList<HotkeyDefinition> BuildConfiguredHotkeys(
        IReadOnlyDictionary<string, string> configuredShortcuts,
        int idBase,
        bool wrapAction)
    {
        var hotkeys = new List<HotkeyDefinition>();
        var actions = BuildHotkeyActions();
        var ordinal = 1;
        foreach (var descriptor in HotkeyActionCatalog.All)
        {
            if (!configuredShortcuts.TryGetValue(descriptor.Id, out var shortcutText))
            {
                ordinal++;
                continue;
            }

            var parsed = HotkeyTextParser.Parse(shortcutText);
            if (!parsed.IsEnabled)
            {
                if (!string.IsNullOrWhiteSpace(parsed.Error))
                {
                    SetStatus($"Shortcut ignored ({descriptor.Name}): {parsed.Error}");
                }

                ordinal++;
                continue;
            }

            var action = actions[descriptor.Id];
            hotkeys.Add(new HotkeyDefinition(
                idBase + ordinal,
                descriptor.Id,
                descriptor.Name,
                parsed.Modifiers,
                parsed.Key,
                parsed.DisplayText,
                wrapAction ? () => RunHotkeyAction(BuildHotkeyAction(descriptor.Id, descriptor.Name, action)) : action));
            ordinal++;
        }

        return hotkeys;
    }

    private Dictionary<string, Action> BuildHotkeyActions()
    {
        return new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
        {
            [KeyboardShortcutSettings.ToggleLens] = ToggleLens,
            [KeyboardShortcutSettings.CaptureLens] = () => _ = CaptureLensAsync(),
            [KeyboardShortcutSettings.NextResultTab] = SelectNextResultTab,
            [KeyboardShortcutSettings.OpenDom] = ShowDomWindow,
            [KeyboardShortcutSettings.OpenCss] = ShowCssWindow,
            [KeyboardShortcutSettings.OpenImages] = () => _ = ShowImagesWindowAsync()
        };
    }

    private static HotkeyDefinition BuildHotkeyAction(string actionId, string name, Action action)
    {
        return new HotkeyDefinition(0, actionId, name, ModifierKeys.None, Key.None, string.Empty, action);
    }

    private void RefreshGlobalHotkeys()
    {
        if (!_isSourceInitialized)
        {
            return;
        }

        _globalHotkeys.Register(this, BuildGlobalHotkeys(), SetStatus);
    }

    private void SelectNextResultTab()
    {
        if (ResultsTabControl.Items.Count == 0)
        {
            return;
        }

        var nextIndex = ResultsTabControl.SelectedIndex < 0
            ? 0
            : (ResultsTabControl.SelectedIndex + 1) % ResultsTabControl.Items.Count;
        ResultsTabControl.SelectedIndex = nextIndex;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
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
        CaptureHistoryTabControl.MaxHeight = 220;
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
        CaptureHistoryTabControl.MaxHeight = double.PositiveInfinity;
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
        IssueTrackerButton.Content = compact ? "Bug" : "Issue";
        PrivacyPreviewButton.Content = compact ? "Safe" : "Privacy";
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
        return settings.Normalized();
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
                new[] { "Evidence capture", "Notes", "Compare", "Report", "Issue" }),
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
                new[] { "Issues", "Attributes", "Notes", "Report", "Issue" })
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
            [IssueTrackerButton] = "Issue",
            [PrivacyPreviewButton] = "Privacy",
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
        HealthPanel.Background = panelBackground;
        HealthPanel.BorderBrush = borderColor;
        ResultsTabControl.Background = tabBackground;
        ResultsTabControl.Foreground = foreground;
        StatusTextBlock.Foreground = mutedText;
        HealthSummaryTextBlock.Foreground = mutedText;
        HealthPanelSummaryTextBlock.Foreground = mutedText;
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

        _settings = NormalizeSettings(window.Settings);
        ApplySettingsToUi();
        await SaveSettingsAsync();
        RefreshGlobalHotkeys();
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
        var selectedDirectory = GetSelectedCapture() is CaptureFileRecord selected
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
            || CaptureFilesDataGrid is null
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
        CaptureFilesDataGrid.ItemsSource = null;
        CaptureFilesDataGrid.ItemsSource = _filteredCaptureFiles;

        if (!string.IsNullOrWhiteSpace(preferredSelection))
        {
            SelectCapture(preferredSelection);
            if (GetSelectedCapture() is null && _filteredCaptureFiles.Count > 0)
            {
                SelectCapture(_filteredCaptureFiles[0]);
            }
        }
        else if (_filteredCaptureFiles.Count > 0 && GetSelectedCapture() is null)
        {
            SelectCapture(_filteredCaptureFiles[0]);
        }

        SetCaptureFilterStatus();
        UpdateCaptureNotesPreview();
    }

    private void SelectCapture(string directory)
    {
        SelectCapture(_filteredCaptureFiles.FirstOrDefault(item =>
            string.Equals(item.DirectoryPath, directory, StringComparison.OrdinalIgnoreCase)));
        UpdateCaptureNotesPreview();
    }

    private void SelectCapture(CaptureFileRecord? capture)
    {
        CaptureFilesListBox.SelectedItem = capture;
        CaptureFilesDataGrid.SelectedItem = capture;
        if (capture is not null)
        {
            CaptureFilesDataGrid.ScrollIntoView(capture);
        }
    }

    private void SyncSelectedCapture(CaptureFileRecord? capture)
    {
        if (capture is null)
        {
            UpdateCaptureNotesPreview();
            return;
        }

        if (!ReferenceEquals(CaptureFilesListBox.SelectedItem, capture))
        {
            CaptureFilesListBox.SelectedItem = capture;
        }

        if (!ReferenceEquals(CaptureFilesDataGrid.SelectedItem, capture))
        {
            CaptureFilesDataGrid.SelectedItem = capture;
        }

        UpdateCaptureNotesPreview();
    }

    private CaptureFileRecord? GetSelectedCapture()
    {
        return CaptureFilesDataGrid?.SelectedItem as CaptureFileRecord
            ?? CaptureFilesListBox?.SelectedItem as CaptureFileRecord;
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
        if (GetSelectedCapture() is not CaptureFileRecord capture)
        {
            CaptureNotesPreviewTextBlock.Text = "No capture selected.";
            UpdateHealthPanel();
            return;
        }

        var notes = LoadCaptureNotes(capture.DirectoryPath);
        CaptureNotesPreviewTextBlock.Text = notes.ShortSummary;
        UpdateHealthPanel();
    }

    private void OpenSelectedCapture()
    {
        if (GetSelectedCapture() is not CaptureFileRecord capture)
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
        if (GetSelectedCapture() is not CaptureFileRecord capture)
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
        if (GetSelectedCapture() is not CaptureFileRecord capture)
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
        if (GetSelectedCapture() is not CaptureFileRecord capture)
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

        var selectedDirectory = GetSelectedCapture() is CaptureFileRecord capture
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
        if (GetSelectedCapture() is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture before exporting a report.");
            return;
        }

        try
        {
            SetBusy(true, "Building polished capture report...");
            var report = CaptureReport.FromDirectory(capture.DirectoryPath, GetActiveUsageProfile().DisplayName)
                .Redacted(GetPrivacyOptions());
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

    private void GenerateIssueTrackerReports()
    {
        if (GetSelectedCapture() is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture before creating issue tracker drafts.");
            return;
        }

        try
        {
            SetBusy(true, "Building issue tracker drafts...");
            var report = CaptureReport.FromDirectory(capture.DirectoryPath, GetActiveUsageProfile().DisplayName)
                .Redacted(GetPrivacyOptions());
            var outputDirectory = Path.Combine(capture.DirectoryPath, "issue-trackers");
            Directory.CreateDirectory(outputDirectory);

            var drafts = IssueTrackerDraftFactory.Create(report, outputDirectory);
            foreach (var draft in drafts)
            {
                File.WriteAllText(
                    draft.FilePath,
                    $"{draft.Title}{Environment.NewLine}{Environment.NewLine}{draft.Body}",
                    Encoding.UTF8);
            }

            var window = new IssueTrackerWindow(drafts, outputDirectory)
            {
                Owner = this,
                Topmost = _settings.Ui.KeepResultWindowsTopmost
            };
            PlaceResultWindow(window);
            window.Show();
            SetStatus($"Issue tracker drafts generated: {outputDirectory}");
        }
        catch (Exception exception)
        {
            SetStatus($"Issue tracker draft failed: {exception.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowPrivacyPreview()
    {
        if (GetSelectedCapture() is not CaptureFileRecord capture)
        {
            SetStatus("Select a capture before opening privacy preview.");
            return;
        }

        try
        {
            var report = CaptureReport.FromDirectory(capture.DirectoryPath, GetActiveUsageProfile().DisplayName);
            var model = PrivacyPreviewModel.Create(
                report,
                GetPrivacyOptions(),
                _settings.Privacy.IncludeScreenshotsInSafeExports);
            var window = new PrivacyPreviewWindow(model, ExportSafePackage)
            {
                Owner = this,
                Topmost = _settings.Ui.KeepResultWindowsTopmost
            };
            PlaceResultWindow(window);
            window.Show();
            SetStatus(model.HasChanges
                ? "Privacy preview opened with redaction findings."
                : "Privacy preview opened; no configured sensitive patterns found.");
        }
        catch (Exception exception)
        {
            SetStatus($"Privacy preview failed: {exception.Message}");
        }
    }

    private string ExportSafePackage(PrivacyPreviewModel model)
    {
        var safeDirectory = Path.Combine(
            model.Original.CaptureDirectory,
            $"privacy-safe-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(safeDirectory);

        var safeReport = model.Redacted with { ScreenshotPath = string.Empty };
        var markdownPath = Path.Combine(safeDirectory, "safe-report.md");
        var htmlPath = Path.Combine(safeDirectory, "safe-report.html");
        var pdfPath = Path.Combine(safeDirectory, "safe-report.pdf");
        File.WriteAllText(markdownPath, safeReport.BuildMarkdown(), Encoding.UTF8);
        File.WriteAllText(htmlPath, safeReport.BuildHtml(), Encoding.UTF8);
        SimplePdfReportWriter.Write(pdfPath, safeReport);

        File.WriteAllText(
            Path.Combine(safeDirectory, "privacy-summary.md"),
            BuildPrivacySummaryMarkdown(model),
            Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "dom.safe.html"), safeReport.Dom, Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "computed.safe.css"), safeReport.ComputedCss, Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "console.safe.txt"), safeReport.Console, Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "attributes.safe.txt"), safeReport.Attributes, Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(safeDirectory, "images.safe.json"),
            JsonSerializer.Serialize(safeReport.Images, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);

        if (model.IncludeScreenshotInSafeExport && File.Exists(model.Original.ScreenshotPath))
        {
            File.Copy(model.Original.ScreenshotPath, Path.Combine(safeDirectory, "screenshot-unredacted.png"), overwrite: true);
        }

        SetStatus($"Safe privacy package exported: {safeDirectory}");
        return safeDirectory;
    }

    private static string BuildPrivacySummaryMarkdown(PrivacyPreviewModel model)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "# Julco Privacy Summary",
                string.Empty,
                model.SummaryText,
                string.Empty,
                "## Policy",
                string.Empty,
                model.IncludeScreenshotInSafeExport
                    ? "Screenshot was included because Settings allows screenshots in safe exports. Review visual content before sharing."
                    : "Screenshot was omitted because safe exports do not include visual captures by default.",
                string.Empty,
                "## Source",
                string.Empty,
                $"- Capture: {model.Original.CaptureDirectory}",
                $"- Page: {model.Redacted.PageTitle}",
                $"- URL: {model.Redacted.PageUrl}",
                $"- Selector: {model.Redacted.Selector}"
            });
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
        IssueTrackerButton.IsEnabled = !isBusy;
        PrivacyPreviewButton.IsEnabled = !isBusy;
        HealthButton.IsEnabled = !isBusy;
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
        UpdateHealthPanel();
    }

    private void ToggleHealthPanel()
    {
        if (HealthPanel.Visibility == Visibility.Visible)
        {
            HealthPanel.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateHealthPanel(forceOpen: true);
    }

    private void UpdateHealthPanel(bool forceOpen = false)
    {
        if (HealthItemsControl is null
            || HealthSummaryTextBlock is null
            || HealthPanelSummaryTextBlock is null)
        {
            return;
        }

        var items = BuildHealthStatusItems();
        HealthItemsControl.ItemsSource = items;
        var warningCount = items.Count(item => item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var okCount = items.Count - warningCount;
        HealthSummaryTextBlock.Text = warningCount == 0
            ? $"Health: OK ({okCount})"
            : $"Health: {warningCount} warning(s)";
        HealthPanelSummaryTextBlock.Text = warningCount == 0
            ? "Julco is ready. Core workflow checks are green."
            : "Review the warning items below before capturing or exporting evidence.";

        if (forceOpen)
        {
            HealthPanel.Visibility = Visibility.Visible;
        }
    }

    private IReadOnlyList<HealthStatusItem> BuildHealthStatusItems()
    {
        return new[]
        {
            BuildBrowserHealth(),
            BuildPortHealth(),
            BuildTabsHealth(),
            BuildInspectionHealth(),
            BuildLensHealth(),
            BuildCaptureFolderHealth(),
            BuildCaptureHistoryHealth(),
            BuildPrivacyHealth(),
            BuildShortcutHealth(),
            BuildProfileHealth()
        };
    }

    private HealthStatusItem BuildBrowserHealth()
    {
        return _activeBrowser is null
            ? Warn("Browser", "Not started", "Open Chrome, Edge, Firefox, or Opera from Julco before inspecting.")
            : Ok("Browser", _activeBrowser.Value.ToString(), $"{_activeBrowser} remote session is the active target family.");
    }

    private HealthStatusItem BuildPortHealth()
    {
        return TryReadPort(out var port)
            ? Ok("Remote port", port.ToString(), $"{PortLabelTextBlock.Text} endpoint configured on localhost:{port}.")
            : Warn("Remote port", "Invalid", "Port must be a number between 1 and 65535.");
    }

    private HealthStatusItem BuildTabsHealth()
    {
        var tabCount = TargetsComboBox?.Items.Count ?? 0;
        if (tabCount <= 0)
        {
            return Warn("Inspectable tabs", "None", "Open browser tabs and press Tabs to refresh target discovery.");
        }

        var selected = TargetsComboBox?.SelectedItem is CdpTarget target
            ? $"{target.Title} - {target.Url}"
            : "No tab selected.";
        return Ok("Inspectable tabs", tabCount.ToString(), selected);
    }

    private HealthStatusItem BuildInspectionHealth()
    {
        return _currentInspection is null
            ? Warn("Inspection", "Idle", "Inspect a selector or use Lens to populate DOM, CSS, console, attributes, images, and issues.")
            : Ok("Inspection", _currentInspection.TagName, $"{_currentInspection.Selector} on {GetSelectedTargetUrl()}");
    }

    private string GetSelectedTargetUrl()
    {
        return TargetsComboBox?.SelectedItem is CdpTarget target
            ? target.Url
            : "-";
    }

    private HealthStatusItem BuildLensHealth()
    {
        if (_lensWindow is null || _lastLensState is null)
        {
            return Warn("Lens", "Inactive", "Open Lens before capturing a framed evidence package.");
        }

        return Ok(
            "Lens",
            _isLensFrozen ? "Frozen" : _lensWindow.IsLocked ? "Locked" : "Active",
            $"Frame {_lastLensState.Bounds.Width:0}x{_lastLensState.Bounds.Height:0}, center {_lastLensState.CenterPoint.X:0},{_lastLensState.CenterPoint.Y:0}. Type: {_lastLensDetectedType}.");
    }

    private HealthStatusItem BuildCaptureFolderHealth()
    {
        try
        {
            var root = GetCaptureRootDirectory();
            Directory.CreateDirectory(root);
            return Ok("Capture folder", "Ready", root);
        }
        catch (Exception exception)
        {
            return Warn("Capture folder", "Blocked", exception.Message);
        }
    }

    private HealthStatusItem BuildCaptureHistoryHealth()
    {
        var selected = GetSelectedCapture();
        var detail = selected is null
            ? $"{_captureFiles.Count} capture(s) loaded. No capture selected."
            : $"{_filteredCaptureFiles.Count}/{_captureFiles.Count} visible. Selected: {selected.HistoryTitle}";
        return _captureFiles.Count == 0
            ? Warn("Capture history", "Empty", "Create a lens capture to start the evidence history.")
            : Ok("Capture history", _captureFiles.Count.ToString(), detail);
    }

    private HealthStatusItem BuildPrivacyHealth()
    {
        var privacy = _settings.Privacy;
        if (!privacy.RedactOnExport)
        {
            return Warn("Privacy", "Off", "Redaction is disabled for exports and issue handoff drafts.");
        }

        var screenshotPolicy = privacy.IncludeScreenshotsInSafeExports
            ? "Safe exports may include unredacted screenshots."
            : "Safe exports omit screenshots by default.";
        return Ok("Privacy", "Protected", $"Redaction is enabled. {screenshotPolicy}");
    }

    private HealthStatusItem BuildShortcutHealth()
    {
        var global = CountEnabledShortcuts(_settings.Keyboard.GlobalShortcuts);
        var local = CountEnabledShortcuts(_settings.Keyboard.LocalShortcuts);
        if (!_settings.Keyboard.EnableGlobalShortcuts && !_settings.Keyboard.EnableLocalShortcuts)
        {
            return Warn("Shortcuts", "Off", "Global and local shortcuts are disabled in Settings.");
        }

        return Ok(
            "Shortcuts",
            $"{global}/{local}",
            $"Global enabled: {_settings.Keyboard.EnableGlobalShortcuts}. Local enabled: {_settings.Keyboard.EnableLocalShortcuts}.");
    }

    private HealthStatusItem BuildProfileHealth()
    {
        if (_usageProfiles.Count == 0)
        {
            return Warn("Profile", "Loading", "Usage profiles are still being initialized.");
        }

        var profile = GetActiveUsageProfile();
        return Ok("Profile", profile.DisplayName, profile.Guidance);
    }

    private static int CountEnabledShortcuts(IReadOnlyDictionary<string, string> shortcuts)
    {
        return shortcuts.Values.Count(value => HotkeyTextParser.Parse(value).IsEnabled);
    }

    private bool TryReadPort(out int port)
    {
        return int.TryParse(PortTextBox.Text, out port) && port is > 0 and <= 65535;
    }

    private static HealthStatusItem Ok(string name, string state, string detail)
    {
        return new HealthStatusItem(name, state, detail, "OK");
    }

    private static HealthStatusItem Warn(string name, string state, string detail)
    {
        return new HealthStatusItem(name, state, detail, "Warning");
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
        string ScreenshotPath,
        string SearchText)
    {
        public string ThumbnailPath => ResolveThumbnailPath(ScreenshotPath);

        public string ThumbnailFallback => File.Exists(ScreenshotPath) ? string.Empty : "No image";

        public string CreatedLocalText => CreatedAt.LocalDateTime.ToString("MM-dd HH:mm");

        public string HistoryTitle => string.IsNullOrWhiteSpace(PageTitle)
            ? Path.GetFileName(DirectoryPath)
            : PageTitle;

        public string HistorySubtitle => string.IsNullOrWhiteSpace(Selector)
            ? $"{TagName}  {Url}"
            : $"{TagName}  {Selector}";

        public string HistoryMeta
        {
            get
            {
                var note = string.IsNullOrWhiteSpace(NoteText)
                    ? string.Empty
                    : $"  notes:{NoteSeverity}/{NoteStatus}";
                return $"{CreatedLocalText}  {Browser}  {ShortenForHistory(Url, 90)}{note}";
            }
        }

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
                            ResolveCaptureFile(directoryPath, evidence.Files.Screenshot),
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
                            ResolveCaptureFile(directoryPath, manifest.Screenshot),
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
                ResolveCaptureFile(directoryPath, "screenshot.png"),
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

        private static string ResolveCaptureFile(string directoryPath, string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            var path = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(directoryPath, relativePath);
            return File.Exists(path) ? path : string.Empty;
        }

        private static string ResolveThumbnailPath(string screenshotPath)
        {
            if (string.IsNullOrWhiteSpace(screenshotPath) || !File.Exists(screenshotPath))
            {
                return string.Empty;
            }

            var thumbnailPath = Path.Combine(
                Path.GetDirectoryName(screenshotPath) ?? string.Empty,
                ".julco-thumbnail.png");
            try
            {
                if (File.Exists(thumbnailPath)
                    && File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(screenshotPath))
                {
                    return thumbnailPath;
                }

                using var source = System.Drawing.Image.FromFile(screenshotPath);
                const int maxWidth = 180;
                const int maxHeight = 120;
                var scale = Math.Min(maxWidth / (double)source.Width, maxHeight / (double)source.Height);
                var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                var height = Math.Max(1, (int)Math.Round(source.Height * scale));
                using var thumbnail = new System.Drawing.Bitmap(maxWidth, maxHeight);
                using var graphics = System.Drawing.Graphics.FromImage(thumbnail);
                graphics.Clear(System.Drawing.Color.FromArgb(10, 14, 20));
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                var x = (maxWidth - width) / 2;
                var y = (maxHeight - height) / 2;
                graphics.DrawImage(source, x, y, width, height);
                thumbnail.Save(thumbnailPath, System.Drawing.Imaging.ImageFormat.Png);
                return thumbnailPath;
            }
            catch
            {
                return screenshotPath;
            }
        }

        private static string ShortenForHistory(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Length <= maxLength
                ? value
                : value[..Math.Max(0, maxLength - 1)] + "...";
        }
    }
}
