using System.Text.Json;
using Julco.Core.Configuration;

namespace Julco.Configuration;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public JsonSettingsStore(string path)
    {
        _path = path;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return AppSettings.Default;
        }

        await using var stream = File.OpenRead(_path);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
            stream,
            SerializerOptions,
            cancellationToken);

        return (settings ?? AppSettings.Default).Normalized();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(
            stream,
            settings.WithProtectedSecrets(),
            SerializerOptions,
            cancellationToken);
    }
}
