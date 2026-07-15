using Julco.Core.Configuration;

namespace Julco.Configuration;

public sealed class InMemorySettingsStore
{
    public AppSettings Current { get; private set; } = AppSettings.Default;

    public void Replace(AppSettings settings)
    {
        Current = settings;
    }
}
