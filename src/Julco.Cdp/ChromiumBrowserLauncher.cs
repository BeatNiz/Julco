using System.Diagnostics;

namespace Julco.Cdp;

public sealed class ChromiumBrowserLauncher
{
    public Process? LaunchChrome(int remoteDebuggingPort, string initialUrl)
    {
        return Launch(FindChromePath(), remoteDebuggingPort, "Chrome", initialUrl);
    }

    public Process? LaunchEdge(int remoteDebuggingPort, string initialUrl)
    {
        return Launch(FindEdgePath(), remoteDebuggingPort, "Edge", initialUrl);
    }

    public Process? LaunchOpera(int remoteDebuggingPort, string initialUrl)
    {
        return Launch(FindOperaPath(), remoteDebuggingPort, "Opera", initialUrl);
    }

    public Process? LaunchFirefox(int remoteDebuggingPort, string initialUrl)
    {
        var executablePath = FindFirefoxPath();
        if (executablePath is null)
        {
            return null;
        }

        var profileDirectory = Path.Combine(Path.GetTempPath(), "Julco", $"Firefox-BiDiProfile-{remoteDebuggingPort}");
        Directory.CreateDirectory(profileDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add($"--remote-debugging-port={remoteDebuggingPort}");
        startInfo.ArgumentList.Add("--no-remote");
        startInfo.ArgumentList.Add("--profile");
        startInfo.ArgumentList.Add(profileDirectory);
        startInfo.ArgumentList.Add("--new-window");
        startInfo.ArgumentList.Add(initialUrl);

        return Process.Start(startInfo);
    }

    private static Process? Launch(string? executablePath, int remoteDebuggingPort, string profileName, string initialUrl)
    {
        if (executablePath is null)
        {
            return null;
        }

        var profileDirectory = Path.Combine(Path.GetTempPath(), "Julco", $"{profileName}-CdpProfile-{remoteDebuggingPort}");
        Directory.CreateDirectory(profileDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--remote-debugging-port={remoteDebuggingPort} --user-data-dir=\"{profileDirectory}\" --new-window \"{initialUrl}\"",
            UseShellExecute = false
        };

        return Process.Start(startInfo);
    }

    private static string? FindChromePath()
    {
        return FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"));
    }

    private static string? FindEdgePath()
    {
        return FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"));
    }

    private static string? FindOperaPath()
    {
        return FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Opera", "opera.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Opera GX", "opera.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Opera", "opera.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Opera", "opera.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Opera GX", "opera.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Opera GX", "opera.exe"));
    }

    private static string? FindFirefoxPath()
    {
        return FirstExisting(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", "firefox.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox", "firefox.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla Firefox", "firefox.exe"));
    }

    private static string? FirstExisting(params string[] paths)
    {
        return paths.FirstOrDefault(File.Exists);
    }
}
