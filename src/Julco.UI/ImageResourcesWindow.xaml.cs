using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Julco.Cdp;

namespace Julco.UI;

public partial class ImageResourcesWindow : Window
{
    private static readonly HttpClient HttpClient = new();
    private IReadOnlyList<WebImageResource> _images;

    public ImageResourcesWindow(IReadOnlyList<WebImageResource> images)
    {
        InitializeComponent();
        _images = images;
        SetImages(images);
    }

    public void SetImages(IReadOnlyList<WebImageResource> images)
    {
        _images = images;
        ImagesListBox.ItemsSource = null;
        ImagesListBox.ItemsSource = _images;
        CountTextBlock.Text = $"Images ({_images.Count})";

        if (_images.Count == 0)
        {
            PreviewImage.Source = null;
            PreviewPlaceholderTextBlock.Text = "No image resources detected in the current inspection.";
            PreviewPlaceholderTextBlock.Visibility = Visibility.Visible;
            DetailsTextBlock.Text = "Move the lens over an element that contains an image or inspect a selector with image resources.";
            return;
        }

        if (ImagesListBox.SelectedItem is null)
        {
            ImagesListBox.SelectedIndex = 0;
        }
    }

    private async void ImagesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImagesListBox.SelectedItem is not WebImageResource image)
        {
            return;
        }

        DetailsTextBlock.Text = $"{image.Kind} | {image.Format} | {image.Width}x{image.Height} | {image.Url}";
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

            PreviewImage.Source = bitmap;
            PreviewPlaceholderTextBlock.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            PreviewPlaceholderTextBlock.Text = $"Preview unavailable. The image can still be saved or opened.\n{exception.Message}";
            PreviewPlaceholderTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void CopyUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (ImagesListBox.SelectedItem is WebImageResource image)
        {
            System.Windows.Clipboard.SetText(image.Url);
            DetailsTextBlock.Text = "Image URL copied.";
        }
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (ImagesListBox.SelectedItem is not WebImageResource image)
        {
            return;
        }

        if (image.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            DetailsTextBlock.Text = "Data URL images can be saved directly, but cannot be opened as a URL.";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = image.Url,
            UseShellExecute = true
        });
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ImagesListBox.SelectedItem is not WebImageResource image)
        {
            return;
        }

        var extension = string.IsNullOrWhiteSpace(image.Format) || image.Format == "unknown"
            ? "img"
            : image.Format;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save image",
            Filter = "Image file|*.*",
            FileName = $"julco-image-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var bytes = await GetImageBytesAsync(image);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            DetailsTextBlock.Text = $"Saved: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            DetailsTextBlock.Text = $"Save failed: {exception.Message}";
        }
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
}
