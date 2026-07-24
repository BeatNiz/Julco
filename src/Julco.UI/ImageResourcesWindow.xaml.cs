using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Julco.Cdp;
using Julco.Core.Privacy;

namespace Julco.UI;

public partial class ImageResourcesWindow : Window
{
    private static readonly HttpClient HttpClient = new();
    private IReadOnlyList<WebImageResource> _images;
    private readonly PrivacyRedactorOptions _privacyOptions;
    private WebImageResource? _selectedImage;
    private byte[]? _selectedBytes;
    private BitmapImage? _selectedBitmap;

    public ImageResourcesWindow(IReadOnlyList<WebImageResource> images, PrivacyRedactorOptions? privacyOptions = null)
    {
        InitializeComponent();
        _privacyOptions = privacyOptions ?? new PrivacyRedactorOptions(false, false, false, false, false, false);
        _images = images;
        SetImages(images);
    }

    public void SetImages(IReadOnlyList<WebImageResource> images)
    {
        _images = images
            .OrderByDescending(image => image.IsLensFrame)
            .ThenBy(image => image.Kind)
            .ToArray();
        ImagesListBox.ItemsSource = null;
        ImagesListBox.ItemsSource = _images;
        CountTextBlock.Text = $"Images ({_images.Count})";

        if (_images.Count == 0)
        {
            PreviewImage.Source = null;
            MetadataGrid.ItemsSource = null;
            PreviewPlaceholderTextBlock.Text = "No image resources detected in the current inspection.";
            PreviewPlaceholderTextBlock.Visibility = Visibility.Visible;
            DetailsTextBlock.Text = "Move the lens over an element that contains an image or inspect a selector with image resources.";
            return;
        }

        ImagesListBox.SelectedIndex = 0;
    }

    private async void ImagesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImagesListBox.SelectedItem is not WebImageResource image)
        {
            return;
        }

        _selectedImage = image;
        _selectedBytes = null;
        _selectedBitmap = null;
        MetadataGrid.ItemsSource = BuildMetadata(image, null, null);
        DetailsTextBlock.Text = BuildDetailsLine(image);
        await LoadPreviewAsync(image);
    }

    private async Task LoadPreviewAsync(WebImageResource image)
    {
        PreviewImage.Source = null;
        PreviewPlaceholderTextBlock.Text = "Loading preview...";
        PreviewPlaceholderTextBlock.Visibility = Visibility.Visible;

        try
        {
            var bytes = await GetImageBytesAsync(image);
            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            _selectedBytes = bytes;
            _selectedBitmap = bitmap;
            PreviewImage.Source = bitmap;
            PreviewPlaceholderTextBlock.Visibility = Visibility.Collapsed;
            MetadataGrid.ItemsSource = BuildMetadata(image, bytes.Length, bitmap);
            DetailsTextBlock.Text = BuildDetailsLine(image, bytes.Length, bitmap);
        }
        catch (Exception exception)
        {
            PreviewPlaceholderTextBlock.Text = $"Preview unavailable. The image can still be saved or opened.\n{exception.Message}";
            PreviewPlaceholderTextBlock.Visibility = Visibility.Visible;
            MetadataGrid.ItemsSource = BuildMetadata(image, null, null);
        }
    }

    private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImage is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(Redact(_selectedImage.Url));
        DetailsTextBlock.Text = _privacyOptions.Enabled ? "Image URL copied with privacy redaction." : "Image URL copied.";
    }

    private void CopyDetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImage is null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(BuildDetailsBlock(_selectedImage, _selectedBytes?.Length, _selectedBitmap));
        DetailsTextBlock.Text = "Image details copied.";
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImage is null)
        {
            return;
        }

        if (_selectedImage.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            DetailsTextBlock.Text = "Data URL and lens frame images can be saved directly, but cannot be opened as a URL.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _selectedImage.Url,
            UseShellExecute = true
        });
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImage is null)
        {
            return;
        }

        var extension = string.IsNullOrWhiteSpace(_selectedImage.Format) || _selectedImage.Format == "unknown"
            ? "img"
            : _selectedImage.Format;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save image",
            Filter = "Image file|*.*",
            FileName = _selectedImage.IsLensFrame
                ? $"julco-lens-frame-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}"
                : $"julco-image-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var bytes = _selectedBytes ?? await GetImageBytesAsync(_selectedImage);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            DetailsTextBlock.Text = $"Saved: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            DetailsTextBlock.Text = $"Save failed: {exception.Message}";
        }
    }

    private IReadOnlyList<ImageMetadataRow> BuildMetadata(
        WebImageResource image,
        long? loadedByteSize,
        BitmapImage? bitmap)
    {
        var naturalWidth = image.NaturalWidth > 0
            ? image.NaturalWidth
            : bitmap?.PixelWidth ?? image.Width;
        var naturalHeight = image.NaturalHeight > 0
            ? image.NaturalHeight
            : bitmap?.PixelHeight ?? image.Height;
        var byteSize = loadedByteSize ?? (image.ByteSize > 0 ? image.ByteSize : null);

        return new[]
        {
            new ImageMetadataRow("Source", image.IsLensFrame ? "Exact lens frame capture" : image.Kind),
            new ImageMetadataRow("Format", image.Format),
            new ImageMetadataRow("Animated", image.IsAnimated ? "Yes" : "No"),
            new ImageMetadataRow("Natural size", naturalWidth > 0 && naturalHeight > 0 ? $"{naturalWidth} x {naturalHeight}" : "Unknown"),
            new ImageMetadataRow("Displayed size", image.DisplayedSizeText),
            new ImageMetadataRow("File weight", byteSize.HasValue ? FormatBytes(byteSize.Value) : "Unknown"),
            new ImageMetadataRow("Alt / label", string.IsNullOrWhiteSpace(image.Alt) ? "-" : Redact(image.Alt)),
            new ImageMetadataRow("URL", Redact(image.Url))
        };
    }

    private static string BuildDetailsLine(WebImageResource image, long? byteSize = null, BitmapImage? bitmap = null)
    {
        var naturalWidth = image.NaturalWidth > 0 ? image.NaturalWidth : bitmap?.PixelWidth ?? image.Width;
        var naturalHeight = image.NaturalHeight > 0 ? image.NaturalHeight : bitmap?.PixelHeight ?? image.Height;
        var natural = naturalWidth > 0 && naturalHeight > 0 ? $"{naturalWidth}x{naturalHeight}" : "unknown";
        var bytes = byteSize ?? (image.ByteSize > 0 ? image.ByteSize : null);
        var weight = bytes.HasValue ? FormatBytes(bytes.Value) : "unknown weight";
        return $"{image.Kind} | {image.Format} | natural {natural} | displayed {image.DisplayedSizeText} | {weight}";
    }

    private string BuildDetailsBlock(WebImageResource image, long? byteSize, BitmapImage? bitmap)
    {
        return string.Join(
            Environment.NewLine,
            BuildMetadata(image, byteSize, bitmap).Select(row => $"{row.Field}: {row.Value}"));
    }

    private string Redact(string value)
    {
        return PrivacyRedactor.RedactText(value, _privacyOptions);
    }

    private static string FormatBytes(long bytes)
    {
        return bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.##} MB"
            : $"{bytes / 1024d:0.#} KB";
    }

    private static async Task<byte[]> GetImageBytesAsync(WebImageResource image)
    {
        if (image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = image.Url.IndexOf(',');
            if (commaIndex < 0)
            {
                throw new InvalidOperationException("Invalid data URL.");
            }

            var metadata = image.Url[..commaIndex];
            var payload = image.Url[(commaIndex + 1)..];
            return metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(payload)
                : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        }

        if (Uri.TryCreate(image.Url, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return await File.ReadAllBytesAsync(uri.LocalPath);
        }

        return await HttpClient.GetByteArrayAsync(image.Url);
    }

    private sealed record ImageMetadataRow(
        string Field,
        string Value);
}
